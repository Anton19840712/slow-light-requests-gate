using lazy_light_requests_gate.core.application.interfaces.databases;
using MongoDB.Driver;
using System.Security.Authentication;

namespace lazy_light_requests_gate.core.application.services.databases
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
				_logger.LogInformation("Reconnecting MongoDB client with new parameters");

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

				// ВАЖНО: Сначала создаем и тестируем новое подключение
				var settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
				settings.SslSettings = new SslSettings { EnabledSslProtocols = SslProtocols.Tls12 };
				settings.ConnectTimeout = TimeSpan.FromSeconds(10);
				settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);

				var newClient = new MongoClient(settings);
				var newDatabase = newClient.GetDatabase(databaseName);

				// Тестируем новое подключение с таймаутом
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
				try
				{
					await newDatabase.RunCommandAsync<MongoDB.Bson.BsonDocument>(
						new MongoDB.Bson.BsonDocument("ping", 1),
						cancellationToken: cts.Token);
				}
				catch (Exception ex)
				{
					// Если новое подключение не работает, закрываем его и выбрасываем исключение
					try { newClient?.Cluster?.Dispose(); } catch { }
					throw new InvalidOperationException($"Failed to test new MongoDB connection: {ex.Message}", ex);
				}

				// Сохраняем ссылку на старые объекты для безопасного закрытия
				var oldClient = _client;
				var oldDatabase = _database;

				// Заменяем на новые ТОЛЬКО после успешного тестирования
				_client = newClient;
				_database = newDatabase;

				// Закрываем старое подключение после замены
				try
				{
					oldClient?.Cluster?.Dispose();
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Error disposing old MongoDB client");
				}

				_logger.LogInformation("MongoDB client reconnected successfully to: {DatabaseName}", databaseName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to reconnect MongoDB client");
				throw;
			}
			finally
			{
				_semaphore.Release();
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

				// Создаем dummy клиент для предотвращения null reference
				var defaultSettings = new MongoClientSettings();
				_client = new MongoClient(defaultSettings);
				_database = _client.GetDatabase("test");
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