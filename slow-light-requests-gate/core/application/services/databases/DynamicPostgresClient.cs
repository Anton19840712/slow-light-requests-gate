using Dapper;
using lazy_light_requests_gate.core.application.interfaces.databases;
using lazy_light_requests_gate.presentation.models.settings.databases;
using Npgsql;
using System.Data;
using System.Text.Json;

namespace lazy_light_requests_gate.core.application.services.databases
{
	public class DynamicPostgresClient : IDynamicPostgresClient
	{
		private string _connectionString;
		private readonly IConfiguration _configuration;
		private readonly ILogger<DynamicPostgresClient> _logger;
		private readonly SemaphoreSlim _semaphore = new(1, 1);

		public DynamicPostgresClient(IConfiguration configuration, ILogger<DynamicPostgresClient> logger)
		{
			_configuration = configuration;
			_logger = logger;

			InitializeFromConfiguration();
		}

		public IDbConnection GetConnection()
		{
			var connection = new NpgsqlConnection(_connectionString);
			connection.Open();
			return connection;
		}

		public string GetConnectionString() => _connectionString;

		public async Task ReconnectAsync(Dictionary<string, object> parameters)
		{
			await _semaphore.WaitAsync();
			try
			{
				_logger.LogInformation("Reconnecting PostgreSQL client with new parameters");

				var host = parameters.GetValueOrDefault("Host", "localhost")?.ToString();

				// по идее ты уже это сделал, когда менял конфигурацию для постгрес.
				var portValue = parameters.GetValueOrDefault("Port", 5432);
				int port;
				if (portValue is JsonElement jsonElement)
				{
					port = jsonElement.GetInt32();
				}
				else
				{
					port = Convert.ToInt32(portValue);
				}
				var username = parameters.GetValueOrDefault("Username", "postgres")?.ToString();
				var password = parameters.GetValueOrDefault("Password", "")?.ToString();
				var database = parameters.GetValueOrDefault("Database", "postgres")?.ToString();

				var connectionStringBuilder = new NpgsqlConnectionStringBuilder
				{
					Host = host,
					Port = port,
					Username = username,
					Password = password,
					Database = database
				};

				var newConnectionString = connectionStringBuilder.ToString();

				// Тестируем новое подключение
				using var testConnection = new NpgsqlConnection(newConnectionString);
				await testConnection.OpenAsync();
				await testConnection.ExecuteScalarAsync("SELECT 1");

				// Заменяем на новое
				_connectionString = newConnectionString;

				_logger.LogInformation("PostgreSQL client reconnected successfully to: {Database}", database);
			}
			finally
			{
				_semaphore.Release();
			}
		}

		private void InitializeFromConfiguration()
		{
			var postgresSection = _configuration.GetSection("PostgresDbSettings");
			var settings = postgresSection.Get<PostgresDbSettings>();

			_connectionString = settings?.GetConnectionString() ??
				throw new InvalidOperationException("PostgreSQL settings not found");
		}
	}
}
