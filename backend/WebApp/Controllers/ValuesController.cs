using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;

namespace WebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ValuesController : ControllerBase
    {
        private readonly ILogger<ValuesController> _logger;
        private readonly IConfiguration _configuration;

        public ValuesController(ILogger<ValuesController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            using (var conn = new NpgsqlConnection(_configuration.GetConnectionString("pgsql")))
            {
                await conn.OpenAsync(HttpContext.RequestAborted);
                var entities = await conn.QueryAsync<ValueEntity>("select id, value, row_version as rowVersion from values");
                return Ok(entities);
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
                    var value = await db.StringGetAsync(id.ToString());
                    if (value.HasValue)
                    {
                        return Ok(value.ToString());
                    }
                }
            }

            using (var conn = new NpgsqlConnection(_configuration.GetConnectionString("pgsql")))
            {
                await conn.OpenAsync(HttpContext.RequestAborted);
                var entity = await conn.QueryFirstOrDefaultAsync<ValueEntity>("select id, value, row_version as rowVersion from values where id = @p0", new {p0 = id});
                if (entity == null)
                {
                    return NotFound();
                }

                return Ok(entity);
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Put([FromRoute] int id, [FromBody] ValueUpdateModel model)
        {
            _logger.LogInformation("Put: id={id}, model={model}", id, model);

            using (var conn = new NpgsqlConnection(_configuration.GetConnectionString("pgsql")))
            {
                await conn.OpenAsync(HttpContext.RequestAborted);
                var entity = await conn.QueryFirstOrDefaultAsync<ValueEntity>("select id, value, row_version as rowVersion from values where id = @p0", new {p0 = id});
                if (entity == null)
                {
                    return NotFound();
                }

                entity.Value = model.Value!;
                var i = await conn.ExecuteAsync("update values set value = @p2 where id = @p0 and row_version = @p1", new {p0 = entity.Id, p1 = entity.RowVersion, p2 = entity.Value});
                return i != 1 ? StatusCode(500) : Ok();
            }
        }
    }

    public class ValueUpdateModel
    {
        [Required]
        [StringLength(32)]
        public string? Value { get; set; }

        public override string ToString()
        {
            return $"Value={Value}";
        }
    }

    public class ValueEntity
    {
        public int Id { get; set; }

        public string Value { get; set; } = string.Empty;

        public DateTimeOffset RowVersion { get; set; }
    }
}