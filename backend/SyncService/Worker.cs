using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;

namespace SyncService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var delay = 0;

            var connection = new HubConnectionBuilder()
                .WithUrl(_configuration.GetValue<string>("HubAddress"))
                .Build();

            connection.On<int>("ReceiveDelay", i =>
            {
                delay = i;
            });

            await connection.StartAsync(stoppingToken);

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            using (var conn = new NpgsqlConnection(_configuration.GetConnectionString("pgsql")))
            {
                await conn.OpenAsync(stoppingToken);
                var redis = await ConnectionMultiplexer.ConnectAsync(_configuration.GetConnectionString("redis"));
                conn.Notification += async (_, e) =>
                {
                    if (delay > 0)
                    {
                        await Task.Delay(delay, stoppingToken);
                    }

                    _logger.LogInformation("Received notification {payload}", e.Payload);

                    try
                    {
                        var model = JsonSerializer.Deserialize<NotificationModel>(e.Payload, options);
                        var db = redis.GetDatabase();
                        if (db != null)
                        {
                            await db.StringSetAsync(model!.Id.ToString(), JsonSerializer.Serialize(model, options));
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "{json}", e.Payload);
                        // Ignored
                    }
                };

                var channelName = _configuration.GetValue<string>("ChannelName");
                var cmd = new NpgsqlCommand($"listen {channelName}", conn);
                _logger.LogInformation("Listen {channelName}", channelName);
                await cmd.ExecuteNonQueryAsync(stoppingToken);

                stoppingToken.WaitHandle.WaitOne();
            }

            await connection.DisposeAsync();
        }
    }

    public class NotificationModel
    {
        public int Id { get; set; }

        public string Value { get; set; } = string.Empty;
    }
}