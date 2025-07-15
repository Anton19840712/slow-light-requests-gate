using System.Security.Authentication;
using System.Text.Json;
using Dapper;
using lazy_light_requests_gate.core.application.interfaces.databases;
using lazy_light_requests_gate.infrastructure.configuration;
using lazy_light_requests_gate.presentation.models.common;
using MongoDB.Driver;
using Npgsql;

namespace lazy_light_requests_gate.infrastructure.services.databases
{
	public class DynamicDatabaseManager : IDynamicDatabaseManager
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly IConfiguration _configuration;
		private readonly ILogger<DynamicDatabaseManager> _logger;
		private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

		private string _currentDatabaseType;
		private Dictionary<string, object> _currentConnectionParameters;

		public DynamicDatabaseManager(
			IServiceProvider serviceProvider,
			IConfiguration configuration,
			ILogger<DynamicDatabaseManager> logger)
		{
			_serviceProvider = serviceProvider;
			_configuration = configuration;
			_logger = logger;
			_currentDatabaseType = configuration["Database"]?.ToLower() ?? "mongo";
			_currentConnectionParameters = new Dictionary<string, object>();
		}

		public async Task SwitchDatabaseAsync(string databaseType)
		{
			await _connectionSemaphore.WaitAsync();
			try
			{
				_logger.LogInformation("Switching database to: {DatabaseType}", databaseType);

				var connectionParams = GetConnectionParametersFromConfig(databaseType);

				var testResult = await TestConnectionAsync(databaseType, connectionParams);
				if (!testResult.IsSuccess)
				{
					throw new InvalidOperationException($"Cannot connect to {databaseType}: {testResult.Message}");
				}

				_currentDatabaseType = databaseType;
				_currentConnectionParameters = connectionParams;

				await UpdateConfigurationAsync(databaseType, connectionParams);

				_logger.LogInformation("Successfully switched to database: {DatabaseType}", databaseType);
			}
			finally
			{
				_connectionSemaphore.Release();
			}
		}

		public async Task ReconnectWithNewParametersAsync(string databaseType, Dictionary<string, object> parameters)
		{
			await _connectionSemaphore.WaitAsync();
			try
			{
				_logger.LogInformation("Reconnecting to {DatabaseType} with new parameters", databaseType);

				// ВАЖНО: Тестируем и перед тестированием обновляем, тестируем подключение ПЕРЕД обновлением клиентов, чтобы проверить валидность данных конфигурации:
				var testResult = await TestConnectionAsync(databaseType, parameters);

				if (!testResult.IsSuccess)
				{
					throw new InvalidOperationException($"Cannot connect to {databaseType}: {testResult.Message}");
				}

				// Сохраняем старые значения для отката в случае ошибки
				var oldDatabaseType = _currentDatabaseType;
				var oldConnectionParameters = new Dictionary<string, object>(_currentConnectionParameters);

				try
				{
					// Обновляем внутреннее состояние полей менеджера по runtime новыми параметрам:
					_currentDatabaseType = databaseType;
					_currentConnectionParameters = parameters;

					// Обновляем конфигурацию системы по базе данных, но ты уже обновлял:
					// await UpdateConfigurationAsync(databaseType, parameters);

					// ТОЛЬКО ПОСЛЕ успешного тестирования обновляем клиенты
					await RecreateConnectionsAsync(databaseType, parameters);

					_logger.LogInformation("Successfully reconnected to {DatabaseType}", databaseType);
				}
				catch (Exception ex)
				{
					// В случае ошибки откатываем состояние
					_currentDatabaseType = oldDatabaseType;
					_currentConnectionParameters = oldConnectionParameters;

					_logger.LogError(ex, "Failed to recreate connections, rolling back");
					throw;
				}
			}
			finally
			{
				_connectionSemaphore.Release();
			}
		}

		public async Task<ConnectionTestResult> TestConnectionAsync(string databaseType, Dictionary<string, object> parameters)
		{
			// пробросились ли параметры
			try
			{
				_logger.LogDebug("Testing connection to {DatabaseType}", databaseType);

				return databaseType.ToLower() switch
				{
					"postgres" => await TestPostgresConnectionAsync(parameters),
					"mongo" => await TestMongoConnectionAsync(parameters),
					_ => new ConnectionTestResult
					{
						IsSuccess = false,
						Message = $"Unsupported database type: {databaseType}"
					},
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error testing connection to {DatabaseType}", databaseType);
				return new ConnectionTestResult
				{
					IsSuccess = false,
					Message = ex.Message
				};
			}
		}

		public Task<object> GetCurrentConnectionInfoAsync()
		{
			var model = new
			{
				DatabaseType = _currentDatabaseType,
				Parameters = _currentConnectionParameters,
				LastConnected = DateTime.UtcNow
			};
			return Task.FromResult((object)model);
		}

		public async Task<DatabaseHealthStatus> CheckHealthAsync()
		{
			try
			{
				var testResult = await TestConnectionAsync(_currentDatabaseType, _currentConnectionParameters);
				return new DatabaseHealthStatus
				{
					IsHealthy = testResult.IsSuccess,
					DatabaseType = _currentDatabaseType,
					Message = testResult.Message,
					LastChecked = DateTime.UtcNow
				};
			}
			catch (Exception ex)
			{
				return new DatabaseHealthStatus
				{
					IsHealthy = false,
					DatabaseType = _currentDatabaseType,
					Message = ex.Message,
					LastChecked = DateTime.UtcNow
				};
			}
		}

		public string GetCurrentDatabaseType()
		{
			return _currentDatabaseType;
		}

		private Dictionary<string, object> GetConnectionParametersFromConfig(string databaseType)
		{
			var parameters = new Dictionary<string, object>();

			switch (databaseType.ToLower())
			{
				case "postgres":
					parameters["Host"] = _configuration["PostgresDbSettings:Host"];
					parameters["Port"] = _configuration.GetValue<int>("PostgresDbSettings:Port");
					parameters["Username"] = _configuration["PostgresDbSettings:Username"];
					parameters["Password"] = _configuration["PostgresDbSettings:Password"];
					parameters["Database"] = _configuration["PostgresDbSettings:Database"];
					break;

				case "mongo":
					parameters["ConnectionString"] = _configuration["MongoDbSettings:ConnectionString"];
					parameters["DatabaseName"] = _configuration["MongoDbSettings:DatabaseName"];
					parameters["User"] = _configuration["MongoDbSettings:User"];
					parameters["Password"] = _configuration["MongoDbSettings:Password"];
					break;
			}

			return parameters;
		}

		/// <summary>
		/// Сначала тестируем соединение
		/// </summary>
		/// <param name="parameters"></param>
		/// <returns></returns>
		private async Task<ConnectionTestResult> TestPostgresConnectionAsync(Dictionary<string, object> parameters)
		{
			try
			{
				// забираем данные из переданных параметров словаря:
				var host = parameters.GetValueOrDefault("host", "localhost")?.ToString();

				var portValue = parameters.GetValueOrDefault("port", 5432);
				int port;
				if (portValue is JsonElement jsonElement)
				{
					port = jsonElement.GetInt32();
				}
				else
				{
					port = Convert.ToInt32(portValue);
				}

				var username = parameters.GetValueOrDefault("username", "postgres")?.ToString();
				var password = parameters.GetValueOrDefault("password", "")?.ToString();
				var database = parameters.GetValueOrDefault("database", "postgres")?.ToString();

				// создаем строку подключения:
				var connectionString = $"Host={host};Port={port};Username={username};Password={password};Database={database};";

				// мы обновляем runtime конфигурацию, потому что мы в ensure database согласно этой конфигурации будем накатывать параметры базы:
				// считаю, что обновлять до метода OpenAsync() не критично
				// первый раз обновляем
				await UpdateConfigurationAsync("postgres", parameters);

				await PostgresDbConfiguration.EnsureDatabaseInitializedAsync(_configuration);

				using var connection = new NpgsqlConnection(connectionString);

				await connection.OpenAsync();

				var version = await connection.ExecuteScalarAsync<string>("SELECT version()");

				return new ConnectionTestResult
				{
					IsSuccess = true,
					Message = "Connection successful",
					ConnectionInfo = new
					{
						Host = host,
						Port = port,
						Database = database,
						Username = username,
						Version = version
					}
				};
			}
			catch (Exception ex)
			{
				return new ConnectionTestResult
				{
					IsSuccess = false,
					Message = ex.Message
				};
			}
		}


		private async Task<ConnectionTestResult> TestMongoConnectionAsync(Dictionary<string, object> parameters)
		{
			try
			{
				string connectionString;
				string database;
				// внимание: должно быть соответствие по ключам из тела запроса:
				var host = parameters.GetValueOrDefault("host", "localhost")?.ToString();
				var portValue = parameters.GetValueOrDefault("port", 27017);
				int port;
				if (portValue is JsonElement jsonElement)
				{
					port = jsonElement.GetInt32();
				}
				else
				{
					port = Convert.ToInt32(portValue);
				}
				var username = parameters.GetValueOrDefault("username", "")?.ToString();
				var password = parameters.GetValueOrDefault("password", "")?.ToString();
				database = parameters.GetValueOrDefault("database", "test")?.ToString();
				var authDatabase = parameters.GetValueOrDefault("authdatabase", "admin")?.ToString();
				connectionString = BuildMongoConnectionString(host, port, username, password, database, authDatabase);

				_logger.LogDebug("Testing MongoDB connection with masked connection string: {ConnectionString}", MaskConnectionString(connectionString));
				var settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
				// Настраиваем SSL если необходимо
				settings.SslSettings = new SslSettings
				{
					EnabledSslProtocols = SslProtocols.Tls12
				};

				settings.ConnectTimeout = TimeSpan.FromSeconds(5);
				settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);

				var testClient = new MongoClient(settings);
				var testDb = testClient.GetDatabase(database);

				// Тестируем подключение с коротким таймаутом
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				await testDb.RunCommandAsync<MongoDB.Bson.BsonDocument>(
					new MongoDB.Bson.BsonDocument("ping", 1),
					cancellationToken: cts.Token);
				var serverVersion = await testDb.RunCommandAsync<MongoDB.Bson.BsonDocument>(
					new MongoDB.Bson.BsonDocument("buildInfo", 1),
					cancellationToken: cts.Token);
				_logger.LogInformation("Successfully connected to MongoDB database: {Database}", database);
				return new ConnectionTestResult
				{
					IsSuccess = true,
					Message = "Connection successful",
					ConnectionInfo = new
					{
						ConnectionString = MaskConnectionString(connectionString),
						Database = database,
						Version = serverVersion.GetValue("version", "unknown").ToString()
					}
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to connect to MongoDB");
				return new ConnectionTestResult
				{
					IsSuccess = false,
					Message = ex.Message
				};
			}
		}

		private string BuildMongoConnectionString(string host, int port, string username, string password, string database, string authDatabase)
		{
			var connectionString = "mongodb://";

			if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
			{
				connectionString += $"{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(password)}@";
			}

			connectionString += $"{host}:{port}";

			if (!string.IsNullOrEmpty(database))
			{
				connectionString += $"/{database}";
			}

			var queryParams = new List<string>();

			if (!string.IsNullOrEmpty(authDatabase) && authDatabase != database)
			{
				queryParams.Add($"authSource={authDatabase}");
			}

			// Добавляем параметры для улучшения стабильности подключения
			queryParams.Add("retryWrites=true");
			queryParams.Add("w=majority");

			if (queryParams.Any())
			{
				connectionString += "?" + string.Join("&", queryParams);
			}

			return connectionString;
		}

		private string MaskConnectionString(string connectionString)
		{
			if (string.IsNullOrEmpty(connectionString))
				return connectionString;

			return System.Text.RegularExpressions.Regex.Replace(
				connectionString,
				@"(:)([^@:]+)(@)",
				"$1***$3");
		}


		// 1 кажется, я в этот метод захожу второй раз:
		// устанавливаем новые параметры в конфигурацию
		private Task UpdateConfigurationAsync(string databaseType, Dictionary<string, object> parameters)
		{
			_configuration["Database"] = databaseType;

			switch (databaseType.ToLower())
			{
				case "postgres":

					var host = parameters.GetValueOrDefault("host", "localhost")?.ToString();
					var port = parameters.GetValueOrDefault("port", 5432)?.ToString();
					var username = parameters.GetValueOrDefault("username", "postgres")?.ToString();
					var password = parameters.GetValueOrDefault("password", "")?.ToString();
					var database = parameters.GetValueOrDefault("database", "postgres")?.ToString();

					_configuration["PostgresDbSettings:Host"] = host;
					_configuration["PostgresDbSettings:Port"] = port;
					_configuration["PostgresDbSettings:Username"] = username;
					_configuration["PostgresDbSettings:Password"] = password;
					_configuration["PostgresDbSettings:Database"] = database;

					break;

				case "mongo":
					if (parameters.ContainsKey("сonnectionstring"))
					{
						_configuration["MongoDbSettings:ConnectionString"] = parameters["сonnectionstring"]?.ToString();
					}

					var databaseName = parameters.GetValueOrDefault("database", "test")?.ToString();
					_configuration["MongoDbSettings:DatabaseName"] = databaseName;

					if (parameters.ContainsKey("username"))
					{
						var userName = parameters["username"]?.ToString();
						_configuration["MongoDbSettings:User"] = userName;
					}
					if (parameters.ContainsKey("password")) 
					{
						var pass = parameters["password"]?.ToString();
						_configuration["MongoDbSettings:Password"] = pass;
					}

					break;
			}

			return Task.CompletedTask;
		}

		private async Task RecreateConnectionsAsync(string databaseType, Dictionary<string, object> parameters)
		{
			switch (databaseType.ToLower())
			{
				case "mongo":
					var dynamicMongoClient = _serviceProvider.GetService<IDynamicMongoClient>();
					if (dynamicMongoClient != null)
					{
						try
						{
							// НЕ тестируем здесь подключение - это уже было сделано в TestConnectionAsync
							await dynamicMongoClient.ReconnectAsync(parameters);
							_logger.LogInformation("MongoDB dynamic client reconnected successfully");
						}
						catch (Exception ex)
						{
							_logger.LogError(ex, "Failed to reconnect MongoDB dynamic client");
							throw; // Пробрасываем исключение для rollback
						}
					}
					break;

				case "postgres":
					var dynamicPostgresClient = _serviceProvider.GetService<IDynamicPostgresClient>();
					if (dynamicPostgresClient != null)
					{
						try
						{
							await dynamicPostgresClient.ReconnectAsync(parameters);
							_logger.LogInformation("PostgreSQL dynamic client reconnected successfully");
						}
						catch (Exception ex)
						{
							_logger.LogError(ex, "Failed to reconnect PostgreSQL dynamic client");
							throw; // Пробрасываем исключение для rollback
						}
					}
					break;

				default:
					_logger.LogWarning("No dynamic client available for database type: {DatabaseType}", databaseType);
					break;
			}
		}
	}
}
