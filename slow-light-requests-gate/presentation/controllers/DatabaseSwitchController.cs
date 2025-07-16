using lazy_light_requests_gate.core.application.helpers;
using lazy_light_requests_gate.core.application.interfaces.databases;
using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.presentation.attributes;
using lazy_light_requests_gate.presentation.models.common;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class DatabaseSwitchController : BaseGatewayController
	{
		private readonly IMessageProcessingServiceFactory _messageProcessingServiceFactory;
		private readonly IDynamicDatabaseManager _databaseManager;
		private readonly IConfiguration _configuration;

		public DatabaseSwitchController(
			IMessageProcessingServiceFactory messageProcessingServiceFactory,
			IDynamicDatabaseManager databaseManager,
			IConfiguration configuration,
			ILogger<DatabaseSwitchController> logger) : base(logger)
		{
			_databaseManager = databaseManager;
			_messageProcessingServiceFactory = messageProcessingServiceFactory;
			_configuration = configuration;
		}

		[HttpPost("switch")]
		[RequireEitherAPIRuntime]
		public async Task<IActionResult> SwitchDatabase([FromBody] DatabaseSwitchRequest request)
		{

			LogOperationStart("SwitchDatabase", request);

			var validation = ValidateRequest(
				(!string.IsNullOrWhiteSpace(request.DatabaseType), "Database type is required")
			);
			if (validation != null) return validation;

			var dbType = DatabaseTypeHelper.Normalize(request.DatabaseType);
			if (dbType != "mongo" && dbType != "postgres")
			{
				return ErrorResponse("Supported database types: 'mongo', 'postgres'", 400, new
				{
					supportedTypes = new[] { "mongo", "postgres" },
					provided = request.DatabaseType
				});
			}

			var previousDbType = _messageProcessingServiceFactory.GetCurrentDatabaseType();

			try
			{
				// Получаем параметры подключения из конфигурации
				var connectionParameters = GetConnectionParametersFromConfig(dbType);

				if (connectionParameters == null || !connectionParameters.Any())
				{
					return ErrorResponse($"No connection configuration found for {dbType}", 400, new
					{
						databaseType = dbType,
						suggestion = "Please check your appsettings.json configuration"
					});
				}

				// Реально подключаемся к новой БД
				await _databaseManager.ReconnectWithNewParametersAsync(dbType, connectionParameters);

				if (request.InitializeSchema || ShouldInitializeSchema(dbType))
				{
					await _databaseManager.InitializeDatabaseSchemaAsync(dbType);
					_logger.LogInformation("Database schema initialized for {DatabaseType}", dbType);
				}

				// Только после успешного подключения меняем тип в factory
				_messageProcessingServiceFactory.SetDefaultDatabaseType(dbType);

				_logger.LogInformation("Database successfully switched from {PreviousType} to {NewType}",
					previousDbType, dbType);

				var result = new
				{
					message = $"Database switched from {previousDbType} to {dbType}",
					previousDatabase = previousDbType,
					currentDatabase = _messageProcessingServiceFactory.GetCurrentDatabaseType(),
					connectionInfo = await _databaseManager.GetCurrentConnectionInfoAsync(),
					switchedAt = DateTime.UtcNow
				};

				LogOperationEnd("SwitchDatabase", result);
				return SuccessResponse(result, "База данных успешно переключена");
			}
			catch (Exception ex)
			{
				// Откатываемся к предыдущему типу при ошибке
				_messageProcessingServiceFactory.SetDefaultDatabaseType(previousDbType);

				_logger.LogError(ex, "Failed to switch database to {DatabaseType}, rolled back to {PreviousType}",
					dbType, previousDbType);

				return ErrorResponse($"Failed to switch to {dbType}: {ex.Message}", 500, new
				{
					databaseType = dbType,
					previousDatabase = previousDbType,
					error = ex.Message,
					rolledBack = true
				});
			};
		}

		[HttpGet("current")]
		[RequireEitherAPIRuntime]
		public async Task<IActionResult> GetCurrentDatabase()
		{
			return await SafeExecuteAsync(async () =>
			{
				LogOperationStart("GetCurrentDatabase");

				var currentDbType = _messageProcessingServiceFactory.GetCurrentDatabaseType();
				var connectionInfo = await _databaseManager.GetCurrentConnectionInfoAsync();

				var result = new
				{
					currentDatabase = currentDbType,
					connectionInfo = connectionInfo,
					checkedAt = DateTime.UtcNow
				};

				LogOperationEnd("GetCurrentDatabase", result);
				return SuccessResponse(result, "Информация о текущей базе данных получена");

			}, "GetCurrentDatabase", "Current database info retrieved");
		}

		[HttpPost("reconnect")]
		[RequireEitherAPIRuntime]
		public async Task<IActionResult> ReconnectWithNewParameters([FromBody] DatabaseReconnectRequest request)
		{
			return await SafeExecuteAsync(async () =>
			{
				LogOperationStart("ReconnectDatabase", request);

				var validation = ValidateRequest(
					(!string.IsNullOrWhiteSpace(request.DatabaseType), "Database type is required"),
					(request.ConnectionParameters != null && request.ConnectionParameters.Any(), "Connection parameters are required")
				);
				if (validation != null) return validation;

				var dbType = DatabaseTypeHelper.Normalize(request.DatabaseType);

				if (dbType != "mongo" && dbType != "postgres")
				{
					return ErrorResponse("Supported database types: 'mongo', 'postgres'", 400, new
					{
						supportedTypes = new[] { "mongo", "postgres" },
						provided = request.DatabaseType
					});
				}

				// Переподключаемся с новыми параметрами
				await _databaseManager.ReconnectWithNewParametersAsync(dbType, request.ConnectionParameters);
				_messageProcessingServiceFactory.SetDefaultDatabaseType(dbType);

				var result = new
				{
					message = $"Database reconnected to {dbType} with new parameters",
					currentDatabase = _messageProcessingServiceFactory.GetCurrentDatabaseType(),
					connectionInfo = await _databaseManager.GetCurrentConnectionInfoAsync(),
					reconnectedAt = DateTime.UtcNow
				};

				LogOperationEnd("ReconnectDatabase", result);
				return SuccessResponse(result, "База данных успешно переподключена");

			}, "ReconnectDatabase", "Database reconnected successfully", request);
		}

		/// <summary>
		/// Тестирование подключения к базе данных
		/// </summary>
		[HttpPost("test")]
		[RequireEitherAPIRuntime]
		public async Task<IActionResult> TestConnection([FromBody] DatabaseTestRequest request)
		{
			return await SafeExecuteAsync(async () =>
			{
				LogOperationStart("TestDatabaseConnection", request);

				var dbType = !string.IsNullOrWhiteSpace(request?.DatabaseType)
					? DatabaseTypeHelper.Normalize(request.DatabaseType)
					: _messageProcessingServiceFactory.GetCurrentDatabaseType();

				if (dbType != "mongo" && dbType != "postgres")
				{
					return ErrorResponse("Supported database types: 'mongo', 'postgres'", 400);
				}

				// Тестируем подключение
				var connectionParameters = request?.ConnectionParameters ?? GetConnectionParametersFromConfig(dbType);
				var testResult = await _databaseManager.TestConnectionAsync(dbType, connectionParameters);

				var result = new
				{
					databaseType = dbType,
					connectionSuccessful = testResult.IsSuccess,
					message = testResult.Message,
					connectionInfo = testResult.ConnectionInfo,
					testedAt = DateTime.UtcNow
				};

				LogOperationEnd("TestDatabaseConnection", result);

				if (testResult.IsSuccess)
				{
					return SuccessResponse(result, "Подключение к базе данных успешно");
				}
				else
				{
					return ErrorResponse("Database connection test failed", 400, result);
				}

			}, "TestDatabaseConnection", "Database connection tested", request);
		}

		#region Private Methods

		/// <summary>
		/// Получение параметров подключения из конфигурации
		/// </summary>
		private Dictionary<string, object> GetConnectionParametersFromConfig(string dbType)
		{
			var parameters = new Dictionary<string, object>();

			switch (dbType)
			{
				case "mongo":
					parameters = GetMongoParametersFromConfig();
					break;
				case "postgres":
					parameters = GetPostgresParametersFromConfig();
					break;
			}

			return parameters;
		}

		/// <summary>
		/// Получение параметров MongoDB из конфигурации
		/// </summary>
		private Dictionary<string, object> GetMongoParametersFromConfig()
		{
			var parameters = new Dictionary<string, object>();

			// Попробуем получить из разных секций конфигурации
			var connectionString = _configuration.GetConnectionString("MongoDB")
				?? _configuration["MongoDbSettings:ConnectionString"]
				?? _configuration["Database:MongoDB:ConnectionString"];

			if (!string.IsNullOrEmpty(connectionString))
			{
				parameters["connectionString"] = connectionString;
			}
			else
			{
				// Если нет готовой строки, собираем из компонентов
				parameters["host"] = _configuration["MongoDbSettings:Host"] ?? _configuration["Database:MongoDB:Host"] ?? "localhost";
				parameters["port"] = int.Parse(_configuration["MongoDbSettings:Port"] ?? _configuration["Database:MongoDB:Port"] ?? "27017");
				parameters["username"] = _configuration["MongoDbSettings:User"] ?? _configuration["Database:MongoDB:User"] ?? "";
				parameters["password"] = _configuration["MongoDbSettings:Password"] ?? _configuration["Database:MongoDB:Password"] ?? "";
				parameters["database"] = _configuration["MongoDbSettings:DatabaseName"] ?? _configuration["Database:MongoDB:DatabaseName"] ?? "test";
				parameters["authdatabase"] = _configuration["MongoDbSettings:AuthDatabase"] ?? _configuration["Database:MongoDB:AuthDatabase"] ?? "admin";
			}

			return parameters;
		}

		/// <summary>
		/// Получение параметров PostgreSQL из конфигурации
		/// </summary>
		private Dictionary<string, object> GetPostgresParametersFromConfig()
		{
			var parameters = new Dictionary<string, object>();
			
			var host = _configuration["PostgresDbSettings:Host"];
			var port = int.Parse(_configuration["PostgresDbSettings:Port"]);
			var username = _configuration["PostgresDbSettings:Username"];
			var password = _configuration["PostgresDbSettings:Password"];
			var database = _configuration["PostgresDbSettings:Database"];

			parameters["host"] = host;
			parameters["port"] = port;
			parameters["username"] = username;
			parameters["password"] = password;
			parameters["database"] = database;

			return parameters;
		}

		/// <summary>
		/// Определяет, нужно ли инициализировать схему БД
		/// </summary>
		private bool ShouldInitializeSchema(string dbType)
		{
			// Для PostgreSQL всегда создаем схему при переключении
			// Для MongoDB схема не нужна (NoSQL)
			return dbType == "postgres";
		}

		#endregion
	}
}
