using lazy_light_requests_gate.core.application.interfaces.databases;
using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.сontrollers
{
	[ApiController]
	[Route("api/[controller]")]
	public class DatabaseSwitchController : ControllerBase
	{
		private readonly IMessageProcessingServiceFactory _messageProcessingServiceFactory;
		private readonly ILogger<DatabaseSwitchController> _logger;
		private readonly IDynamicDatabaseManager _databaseManager;
		private readonly IConfiguration _configuration;
		public DatabaseSwitchController(
			IMessageProcessingServiceFactory messageProcessingServiceFactory,
			IDynamicDatabaseManager databaseManager,
			IConfiguration configuration,
			ILogger<DatabaseSwitchController> logger)
		{
			_databaseManager = databaseManager;
			_messageProcessingServiceFactory = messageProcessingServiceFactory;
			_configuration = configuration;
			_logger = logger;
		}

		[HttpPost("switch")]
		public IActionResult SwitchDatabase([FromBody] DatabaseSwitchRequest request)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(request.DatabaseType))
				{
					return BadRequest("Database type is required");
				}

				var dbType = request.DatabaseType.ToLower();
				if (dbType != "mongo" && dbType != "postgres")
				{
					return BadRequest("Supported database types: 'mongo', 'postgres'");
				}

				_messageProcessingServiceFactory.SetDefaultDatabaseType(dbType);

				_logger.LogInformation("Database switched to: {DatabaseType}\n", dbType);

				return Ok(new
				{
					message = $"Database switched to {dbType}",
					currentDatabase = _messageProcessingServiceFactory.GetCurrentDatabaseType()
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error switching database");
				return StatusCode(500, "Internal server error");
			}
		}

		[HttpGet("current")]
		public IActionResult GetCurrentDatabase()
		{
			return Ok(new
			{
				currentDatabase = _messageProcessingServiceFactory.GetCurrentDatabaseType()
			});
		}

		[HttpPost("reconnect")]
		public async Task<IActionResult> ReconnectWithNewParameters([FromBody] DatabaseReconnectRequest request)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(request.DatabaseType))
					return BadRequest("Database type is required");

				// ИЗМЕНЕНИЕ: Нормализуем тип БД сразу
				var dbType = DatabaseTypeHelper.Normalize(request.DatabaseType);

				if (dbType != "mongo" && dbType != "postgres")
					return BadRequest("Supported database types: 'mongodb', 'postgres'");

				// ИЗМЕНЕНИЕ: Передаем уже нормализованный тип
				await _databaseManager.ReconnectWithNewParametersAsync(dbType, request.ConnectionParameters);

				// ИЗМЕНЕНИЕ: Устанавливаем нормализованный тип БД
				_messageProcessingServiceFactory.SetDefaultDatabaseType(dbType);

				_logger.LogInformation("Database reconnected to: {DatabaseType} with new parameters", dbType);

				return Ok(new
				{
					message = $"Database reconnected to {dbType} with new parameters",
					currentDatabase = _messageProcessingServiceFactory.GetCurrentDatabaseType(),
					connectionInfo = await _databaseManager.GetCurrentConnectionInfoAsync()
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error reconnecting database with new parameters");
				return StatusCode(500, new { message = "Internal server error", details = ex.Message });
			}
		}
	}

	public class DatabaseSwitchRequest
	{
		public string DatabaseType { get; set; }
	}

	public class DatabaseReconnectRequest
	{
		public string DatabaseType { get; set; }
		public Dictionary<string, object> ConnectionParameters { get; set; }
		public bool InitializeSchema { get; set; } = false;

	}
	public static class DatabaseTypeHelper
	{
		public static string Normalize(string databaseType)
		{
			return databaseType?.ToLowerInvariant() switch
			{
				"mongodb" => "mongo",
				"mongo" => "mongo",
				"postgresql" => "postgres",
				"postgres" => "postgres",
				_ => databaseType?.ToLowerInvariant()
			};
		}
	}
}
