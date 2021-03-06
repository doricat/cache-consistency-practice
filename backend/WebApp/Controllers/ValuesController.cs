using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;
using WebApi.Models;
using WebApp.Hubs;

namespace WebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ValuesController : ControllerBase
    {
        private readonly ILogger<ValuesController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<SettingHub, ISettingHub> _hubContext;

        public ValuesController(ILogger<ValuesController> logger, 
            IConfiguration configuration, 
            IHubContext<SettingHub, ISettingHub> hubContext)
        {
            _logger = logger;
            _configuration = configuration;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            using (var conn = new NpgsqlConnection(_configuration.GetConnectionString("pgsql")))
            {
                await conn.OpenAsync(HttpContext.RequestAborted);
                var entities = await conn.QueryAsync<ValueEntity>("select id, value, xmin as RowVersion from values order by id");
                return Ok(new ApiResult<IList<ValueEntity>>(entities.ToList()));
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get([FromRoute] int id)
        {
            _logger.LogInformation("Get: id={id}", id);

            using (var redis = await ConnectionMultiplexer.ConnectAsync(_configuration.GetConnectionString("redis")))
            {
                var db = redis.GetDatabase();
                if (db != null)
                {
                    var value = await db.StringGetAsync($"cache-consistency-practice-{id}");
                    if (value.HasValue)
                    {
                        var entity = JsonSerializer.Deserialize<ValueEntity>(value.ToString(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                        return Ok(new ApiResult<ValueEntity>(entity!));
                    }
                }
            }

            using (var conn = new NpgsqlConnection(_configuration.GetConnectionString("pgsql")))
            {
                await conn.OpenAsync(HttpContext.RequestAborted);
                var entity = await conn.QueryFirstOrDefaultAsync<ValueEntity>("select id, value, xmin as RowVersion from values where id = @p0", new {p0 = id});
                if (entity == null)
                {
                    return NotFound();
                }

                return Ok(new ApiResult<ValueEntity>(entity));
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Put([FromRoute] int id, [FromBody] ValueUpdateModel model)
        {
            _logger.LogInformation("Put: id={id}, model={model}", id, model);

            await _hubContext.Clients.All.ReceiveDelay(model.Delay);

            using (var conn = new NpgsqlConnection(_configuration.GetConnectionString("pgsql")))
            {
                await conn.OpenAsync(HttpContext.RequestAborted);
                var entity = await conn.QueryFirstOrDefaultAsync<ValueEntity>("select id, value, xmin as RowVersion from values where id = @p0", new {p0 = id});
                if (entity == null)
                {
                    return NotFound();
                }

                entity.Value = model.Value!;
                var i = await conn.ExecuteAsync("update values set value = @p2 where id = @p0 and xmin = @p1", new {p0 = entity.Id, p1 = entity.RowVersion, p2 = entity.Value});
                return i != 1 ? StatusCode(500) : NoContent();
            }
        }
    }

    public class ValueUpdateModel
    {
        [Required]
        [StringLength(50)]
        public string? Value { get; set; }

        public int Delay { get; set; }

        public override string ToString()
        {
            return $"{{Value={Value}, Delay={Delay}}}";
        }
    }

    public class ValueEntity
    {
        public int Id { get; set; }

        public string Value { get; set; } = string.Empty;

        public int RowVersion { get; set; }
    }
}