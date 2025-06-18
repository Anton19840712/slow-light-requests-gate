using Newtonsoft.Json.Linq;

namespace lazy_light_requests_gate.middleware;

/// <summary>
/// Класс используется для предоставления возможности настройщику системы
/// динамически задавать хост и порт самого динамического шлюза.
/// </summary>
public static class GateConfiguration
{
	/// <summary>
	/// Настройка динамических параметров шлюза и возврат HTTP/HTTPS адресов
	/// </summary>
	public static async Task<(string HttpUrl, string HttpsUrl)> ConfigureDynamicGateAsync(string[] args, WebApplicationBuilder builder)
	{
		var configFilePath = args.FirstOrDefault(a => a.StartsWith("--config="))?.Substring(9) ?? "rest.json";
		var config = LoadConfiguration(configFilePath);

		var configType = config["type"]?.ToString() ?? config["Type"]?.ToString();
		return await ConfigureRestGate(config, builder);
	}

	private static async Task<(string HttpUrl, string HttpsUrl)> ConfigureRestGate(JObject config, WebApplicationBuilder builder)
	{
		var companyName = config["CompanyName"]?.ToString() ?? "default-company";
		var host = config["Host"]?.ToString() ?? "localhost";
		var port = int.TryParse(config["Port"]?.ToString(), out var p) ? p : 5000;
		var enableValidation = bool.TryParse(config["Validate"]?.ToString(), out var v) && v;
		var database = config["Database"]?.ToString() ?? "mongo";
		var bus = config["Bus"]?.ToString() ?? "rabbit";
		var cleanupIntervalSeconds = int.TryParse(config["CleanupIntervalSeconds"]?.ToString(), out var c) ? c : 10;

		builder.Configuration["CompanyName"] = companyName;
		builder.Configuration["Host"] = host;
		builder.Configuration["Port"] = port.ToString();
		builder.Configuration["Validate"] = enableValidation.ToString();
		builder.Configuration["Database"] = database;
		builder.Configuration["Bus"] = bus;
		builder.Configuration["CleanupIntervalSeconds"] = cleanupIntervalSeconds.ToString();

		// Настройка PostgreSQL из rest.json
		var postgresSettings = config["PostgresDbSettings"];
		if (postgresSettings != null)
		{
			builder.Configuration["PostgresDbSettings:Host"] = postgresSettings["Host"]?.ToString() ?? "localhost";
			builder.Configuration["PostgresDbSettings:Port"] = postgresSettings["Port"]?.ToString() ?? "5432";
			builder.Configuration["PostgresDbSettings:Username"] = postgresSettings["Username"]?.ToString() ?? "postgres";
			builder.Configuration["PostgresDbSettings:Password"] = postgresSettings["Password"]?.ToString() ?? "";
			builder.Configuration["PostgresDbSettings:Database"] = postgresSettings["Database"]?.ToString() ?? "GatewayDB";
		}

		// Настройка MongoDB из rest.json
		var mongoSettings = config["MongoDbSettings"];
		if (mongoSettings != null)
		{
			builder.Configuration["MongoDbSettings:User"] = mongoSettings["User"]?.ToString() ?? "";
			builder.Configuration["MongoDbSettings:Password"] = mongoSettings["Password"]?.ToString() ?? "";
			builder.Configuration["MongoDbSettings:ConnectionString"] = mongoSettings["ConnectionString"]?.ToString() ?? "";
			builder.Configuration["MongoDbSettings:DatabaseName"] = mongoSettings["DatabaseName"]?.ToString() ?? "GatewayDB";

			var collections = mongoSettings["Collections"];
			if (collections != null)
			{
				builder.Configuration["MongoDbSettings:Collections:QueueCollection"] = collections["QueueCollection"]?.ToString() ?? "QueueEntities";
				builder.Configuration["MongoDbSettings:Collections:OutboxCollection"] = collections["OutboxCollection"]?.ToString() ?? "OutboxMessages";
				builder.Configuration["MongoDbSettings:Collections:IncidentCollection"] = collections["IncidentCollection"]?.ToString() ?? "IncidentEntities";
			}
		}

		// Настройка RabbitMQ из rest.json
		var rabbitSettings = config["RabbitMqSettings"];
		if (rabbitSettings != null)
		{
			builder.Configuration["RabbitMqSettings:HostName"] = rabbitSettings["HostName"]?.ToString() ?? "localhost";
			builder.Configuration["RabbitMqSettings:Port"] = rabbitSettings["Port"]?.ToString() ?? "5672";
			builder.Configuration["RabbitMqSettings:UserName"] = rabbitSettings["UserName"]?.ToString() ?? "guest";
			builder.Configuration["RabbitMqSettings:Password"] = rabbitSettings["Password"]?.ToString() ?? "guest";
			builder.Configuration["RabbitMqSettings:VirtualHost"] = rabbitSettings["VirtualHost"]?.ToString() ?? "/";
		}

		// Настройка Kafka из rest.json
		var kafkaSettings = config["KafkaSettings"];
		if (kafkaSettings != null)
		{
			builder.Configuration["KafkaSettings:BootstrapServers"] = kafkaSettings["BootstrapServers"]?.ToString() ?? "localhost:9092";
			builder.Configuration["KafkaSettings:GroupId"] = kafkaSettings["GroupId"]?.ToString() ?? "slow-light-requests-gate-group";
			builder.Configuration["KafkaSettings:SecurityProtocol"] = kafkaSettings["SecurityProtocol"]?.ToString() ?? "Plaintext";
			builder.Configuration["KafkaSettings:SessionTimeoutMs"] = kafkaSettings["SessionTimeoutMs"]?.ToString() ?? "6000";
			builder.Configuration["KafkaSettings:EnableAutoCommit"] = kafkaSettings["EnableAutoCommit"]?.ToString() ?? "true";
		}

		// Логирование конфигурации
		Console.WriteLine($"[INFO] Конфигурация загружена:");
		Console.WriteLine($"[INFO] - Company: {companyName}");
		Console.WriteLine($"[INFO] - Host: {host}:{port}");
		Console.WriteLine($"[INFO] - Database: {database}");
		Console.WriteLine($"[INFO] - Bus: {bus}");
		Console.WriteLine($"[INFO] - Validation: {enableValidation}");
		Console.WriteLine($"[INFO] - Cleanup Interval: {cleanupIntervalSeconds} seconds");

		// ports here were hardcoded:
		var httpUrl = $"http://{host}:80";
		var httpsUrl = $"https://{host}:443";
		return await Task.FromResult((httpUrl, httpsUrl));
	}

	private static JObject LoadConfiguration(string configFilePath)
	{
		try
		{
			string fullPath;

			if (Path.IsPathRooted(configFilePath))
			{
				fullPath = configFilePath;
			}
			else
			{
				var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
				fullPath = Path.GetFullPath(Path.Combine(basePath, configFilePath));
			}

			// Печатаем информацию для отладки.
			Console.WriteLine();
			Console.WriteLine($"[INFO] Конечный путь к конфигу: {fullPath}");
			Console.WriteLine($"[INFO] Загружается конфигурация: {Path.GetFileName(fullPath)}");
			Console.WriteLine();

			// Проверка существования файла.
			if (!File.Exists(fullPath))
				throw new FileNotFoundException("Файл конфигурации не найден", fullPath);

			// Загружаем конфигурацию из файла.
			var json = File.ReadAllText(fullPath);
			return JObject.Parse(json);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Ошибка при загрузке конфигурации из файла '{configFilePath}': {ex.Message}");
		}
	}
}
