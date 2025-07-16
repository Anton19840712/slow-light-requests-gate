using lazy_light_requests_gate.core.application.interfaces.databases;
using MongoDB.Driver;
using System.Security.Authentication;

namespace lazy_light_requests_gate.infrastructure.services.databases
{
	public class DynamicMongoClient : IDynamicMongoClient
	{
		private IMongoClient _client;
		private IMongoDatabase _database;
		private readonly IConfiguration _configuration;
		private readonly ILogger<DynamicMongoClient> _logger;
		private readonly SemaphoreSlim _semaphore = new(1, 1);

		public DynamicMongoClient(IConfiguration configuration, ILogger<DynamicMongoClient> logger)
		{
			_configuration = configuration;
			_logger = logger;

			// Инициализируем с текущей конфигурацией
			InitializeFromConfiguration();
		}

		public IMongoClient GetClient() => _client;
		public IMongoDatabase GetDatabase() => _database;
		public IMongoDatabase GetDatabase(string databaseName) => _client?.GetDatabase(databaseName);

		public async Task ReconnectAsync(Dictionary<string, object> parameters)
		{
			await _semaphore.WaitAsync();
			try
			{
				_logger.LogInformation("Recreating MongoDB client with new parameters");

				var connectionString = parameters.GetValueOrDefault("ConnectionString")?.ToString();
				if (string.IsNullOrEmpty(connectionString))
				{
					var host = parameters.GetValueOrDefault("Host", "localhost")?.ToString();

					var portValue = parameters.GetValueOrDefault("Port", 27017);
					int port;
					if (portValue is System.Text.Json.JsonElement jsonElement)
					{
						port = jsonElement.GetInt32();
					}
					else
					{
						port = Convert.ToInt32(portValue);
					}

					var username = parameters.GetValueOrDefault("User", "")?.ToString() ??
								  parameters.GetValueOrDefault("UserName", "")?.ToString();
					var password = parameters.GetValueOrDefault("Password", "")?.ToString();
					var database = parameters.GetValueOrDefault("DatabaseName", "test")?.ToString();
					var authDatabase = parameters.GetValueOrDefault("AuthDatabase", "admin")?.ToString();

					connectionString = BuildMongoConnectionString(host, port, username, password, database, authDatabase);
				}

				var databaseName = parameters.GetValueOrDefault("DatabaseName", "test")?.ToString();

				// Закрываем старое соединение СНАЧАЛА
				await DisposeCurrentConnectionAsync();

				// Создаем новое подключение БЕЗ тестирования
				var settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
				settings.SslSettings = new SslSettings { EnabledSslProtocols = SslProtocols.Tls12 };
				settings.ConnectTimeout = TimeSpan.FromSeconds(10);
				settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);

				var newClient = new MongoClient(settings);
				var newDatabase = newClient.GetDatabase(databaseName);

				// Устанавливаем новое соединение БЕЗ предварительного тестирования
				_client = newClient;
				_database = newDatabase;

				_logger.LogInformation("MongoDB client recreated successfully for database: {DatabaseName}", databaseName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to recreate MongoDB client");
				throw;
			}
			finally
			{
				_semaphore.Release();
			}
		}

		private async Task DisposeCurrentConnectionAsync()
		{
			if (_client != null)
			{
				_logger.LogInformation("Disposing current MongoDB connection...");

				try
				{
					// Даем время завершить текущие операции
					await Task.Delay(1000);

					_client.Cluster?.Dispose();
					_logger.LogInformation("MongoDB connection disposed successfully");
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Error disposing MongoDB client");
				}
				finally
				{
					_client = null;
					_database = null;
				}
			}
		}

		private void InitializeFromConfiguration()
		{
			try
			{
				var connectionString = _configuration["MongoDbSettings:ConnectionString"];
				var databaseName = _configuration["MongoDbSettings:DatabaseName"];

				if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(databaseName))
				{
					_logger.LogWarning("MongoDB configuration is incomplete, using default values");
					connectionString = "mongodb://localhost:27017";
					databaseName = "test";
				}

				var settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
				settings.SslSettings = new SslSettings { EnabledSslProtocols = SslProtocols.Tls12 };
				settings.ConnectTimeout = TimeSpan.FromSeconds(10);
				settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);

				_client = new MongoClient(settings);
				_database = _client.GetDatabase(databaseName);

				_logger.LogInformation("MongoDB client initialized for database: {DatabaseName}", databaseName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to initialize MongoDB client from configuration");
				_client = null;
				_database = null;
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

		public void Dispose()
		{
			try
			{
				_client?.Cluster?.Dispose();
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error disposing MongoDB client");
			}
		}
	}
}
