using lazy_light_requests_gate.core.domain.settings.common;
using lazy_light_requests_gate.presentation.enums;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.infrastructure.configuration;

/// <summary>
/// Класс используется для предоставления возможности настройщику системы
/// динамически задавать хост и порт самого динамического шлюза.
/// </summary>
public static class GateConfiguration
{
	// Поддерживаемые шины сообщений
	private static readonly string[] SupportedMessageBuses = { "rabbit", "activemq", "pulsar", "tarantool", "kafkastreams" };
	
	// Поддерживаемые протоколы
	private static readonly string[] SupportedProtocols = { "tcp", "udp", "websockets" };

	// Поддерживаемые форматы данных
	private static readonly string[] SupportedDataFormats = { "json", "xml", "binary", "protobuf" };
	
	// Поддерживаемые базы данных  
	private static readonly string[] SupportedDatabases = { "postgres", "mongo" };

	private static readonly string[] SupportedApplicationTypes = { "restonly", "streamonly", "both" };

	private static string _inPushChannel;
	private static string _outPullChannel;

	/// <summary>
	/// Настройка динамических параметров шлюза и возврат HTTP/HTTPS адресов
	/// </summary>
	public static async Task<(string HttpUrl, string HttpsUrl)> ConfigureDynamicGateAsync(string[] args, WebApplicationBuilder builder)
	{
		var configFilePath = args.FirstOrDefault(a => a.StartsWith("--config="))?.Substring(9) ?? "gate-config.json";
		var config = LoadConfiguration(configFilePath);

		// Валидируем базовую структуру конфигурации
		ValidateBasicConfiguration(config);

		var configType = config["type"]?.ToString() ?? config["Type"]?.ToString();
		return await ConfigureRestGate(config, builder);
	}
	/// <summary>
	/// Нормализует значения конфигурации к нижнему регистру
	/// </summary>
	private static void NormalizeConfigurationValues(JObject config)
	{
		var fieldsToNormalize = new[] { "Database", "Bus", "Protocol", "DataFormat" };

		foreach (var field in fieldsToNormalize)
		{
			if (config[field] != null)
			{
				config[field] = config[field].ToString().ToLowerInvariant();
			}
		}
	}
	private static Mode GetMode(bool client, bool server)
	{
		if (client)
			return Mode.Client;
		if (server)
			return Mode.Server;
		return Mode.Unknown;
	}

	/// <summary>
	/// Устанавливаем параметры конфигурационного шлюза.
	/// </summary>
	/// <param name="config"></param>
	/// <param name="builder"></param>
	/// <returns></returns>
	private static async Task<(string HttpUrl, string HttpsUrl)> ConfigureRestGate(JObject config, WebApplicationBuilder builder)
	{
		// Настройка DataOptions
		var dataOptions = config["DataOptions"];
		if (dataOptions != null)
		{
			var client = bool.TryParse(dataOptions["client"]?.ToString(), out var clientParsed) && clientParsed;
			var server = bool.TryParse(dataOptions["server"]?.ToString(), out var serverParsed) && serverParsed;

			builder.Configuration["DataOptions:client"] = client.ToString();
			builder.Configuration["DataOptions:server"] = server.ToString();

			// Вычисляем и устанавливаем Mode
			var mode = GetMode(client, server);
			builder.Configuration["Mode"] = mode.ToString();

			var serverDetails = dataOptions["serverDetails"];
			if (serverDetails != null)
			{
				builder.Configuration["DataOptions:serverDetails:host"] = serverDetails["host"]?.ToString() ?? "127.0.0.1";
				builder.Configuration["DataOptions:serverDetails:port"] = serverDetails["port"]?.ToString() ?? "6254";
			}

			var clientDetails = dataOptions["clientDetails"];
			if (clientDetails != null)
			{
				builder.Configuration["DataOptions:clientDetails:host"] = clientDetails["host"]?.ToString() ?? "127.0.0.1";
				builder.Configuration["DataOptions:clientDetails:port"] = clientDetails["port"]?.ToString() ?? "5001";
			}
		}

		var companyName = config["CompanyName"]?.ToString() ?? "default-company";
		var host = config["Host"]?.ToString() ?? "localhost";
		var portHttp = int.TryParse(config["PortHttp"]?.ToString(), out var ph) ? ph : 80;
		var portHttps = int.TryParse(config["PortHttps"]?.ToString(), out var phs) ? phs : 443;
		var enableValidation = bool.TryParse(config["Validate"]?.ToString(), out var v) && v;
		var database = config["Database"]?.ToString() ?? "mongo";
		var bus = config["Bus"]?.ToString() ?? "rabbit";
		var protocol = config["Protocol"]?.ToString() ?? "tcp";
		var dataFormat = config["DataFormat"]?.ToString() ?? "json";
		var cleanupIntervalSeconds = int.TryParse(config["CleanupIntervalSeconds"]?.ToString(), out var c) ? c : 10; 
		var outboxMessageTtlSeconds = int.TryParse(config["OutboxMessageTtlSeconds"]?.ToString(), out var ttl) ? ttl : 10;
		var incidentEntitiesTtlMonths = int.TryParse(config["IncidentEntitiesTtlMonths"]?.ToString(), out var incident) ? incident : 10;
		var applicationType = config["Type"]?.ToString()?.ToLowerInvariant() ?? "restonly";

		// устанавливаем название очередей для дальнейшейго переиспользования в классе:
		_inPushChannel = config["InputChannel"]?.ToString() ?? "default-in-channel";
		_outPullChannel = config["OutputChannel"]?.ToString() ?? "default-out-channel";
		_outPullChannel = config["OutputChannel"]?.ToString() ?? "default-out-channel";

		//НОРМАЛИЗУЕМ JSON ПЕРЕД ПАРСИНГОМ В МОДЕЛЬ
		NormalizeConfigurationValues(config);

		// Валидируем выбранные параметры
		ValidateConfiguration(database, bus, protocol, dataFormat, config);

		// Парсим в strongly typed модель для дополнительной валидации
		var gateConfig = ParseToStronglyTypedModel(config);
		ValidateGateConfigurationModel(gateConfig);

		// Настройка шины сообщений
		var busSettings = GetBusSettings(bus, config);
		if (busSettings is JObject settingsObject)
		{
			foreach (var prop in settingsObject.Properties())
			{
				var key = $"BusSettings:{prop.Name}";
				var value = prop.Value?.ToString();
				if (!string.IsNullOrEmpty(value))
					builder.Configuration[key] = value;
			}
		}

		// Основные параметры конфигурации
		builder.Configuration["CompanyName"] = companyName;

		builder.Configuration["Host"] = host;
		builder.Configuration["PortHttp"] = portHttp.ToString();
		builder.Configuration["PortHttps"] = portHttps.ToString();
		builder.Configuration["Validate"] = enableValidation.ToString();
		builder.Configuration["Database"] = database;
		builder.Configuration["Bus"] = bus;
		builder.Configuration["Protocol"] = protocol;
		builder.Configuration["DataFormat"] = dataFormat;
		builder.Configuration["CleanupIntervalSeconds"] = cleanupIntervalSeconds.ToString(); 
		builder.Configuration["OutboxMessageTtlSeconds"] = outboxMessageTtlSeconds.ToString();
		builder.Configuration["IncidentEntitiesTtlMonths"] = incidentEntitiesTtlMonths.ToString();

		builder.Configuration["ApplicationType"] = applicationType;
		if (!SupportedApplicationTypes.Contains(applicationType))
		{
			throw new InvalidOperationException($"Неподдерживаемый тип приложения: {applicationType}. Поддерживаются: {string.Join(", ", SupportedApplicationTypes)}");
		}

		// частные для внутреннего использования названия:
		builder.Configuration["ProtocolTcpValue"] = "tcp";
		builder.Configuration["ProtocolUdpValue"] = "udp";
		builder.Configuration["ProtocolWsValue"] = "ws";

		builder.Configuration["Inputchannel"] = _inPushChannel;
		builder.Configuration["OutputChannel"] = _outPullChannel;

		// Настройка базы данных
		await ConfigureDatabase(config, builder, database);

		// Настройка шины сообщений  
		await ConfigureMessageBus(config, builder, bus);

		// Красивое логирование конфигурации
		LogDetailedConfiguration(companyName, host, portHttp, portHttps, enableValidation, database, bus,
				cleanupIntervalSeconds, outboxMessageTtlSeconds, incidentEntitiesTtlMonths, protocol, dataFormat, applicationType, config);


		// ports here were hardcoded:
		var httpUrl = $"http://{host}:{portHttp}";
		var httpsUrl = $"https://{host}:{portHttps}";
		return await Task.FromResult((httpUrl, httpsUrl));
	}

	/// <summary>
	/// Получить настройки шины сообщений по типу
	/// </summary>
	private static JToken GetBusSettings(string bus, JObject config)
	{
		return bus.ToLower() switch
		{
			"rabbit" => config["RabbitMqSettings"],
			"activemq" => config["ActiveMqSettings"],
			"pulsar" => config["PulsarSettings"],
			"tarantool" => config["TarantoolSettings"],
			"kafkastreams" => config["KafkaStreamsSettings"],
			_ => throw new InvalidOperationException($"Bus '{bus}' не поддерживается. Поддерживаются: {string.Join(", ", SupportedMessageBuses)}")
		};
	}

	/// <summary>
	/// Получить ключ настроек для валидации
	/// </summary>
	private static string GetBusSettingsKey(string bus)
	{
		return bus.ToLower() switch
		{
			"rabbit" => "RabbitMqSettings",
			"activemq" => "ActiveMqSettings",
			"pulsar" => "PulsarSettings",
			"tarantool" => "TarantoolSettings",
			"kafkastreams" => "KafkaStreamsSettings",
			_ => throw new InvalidOperationException($"Bus '{bus}' не поддерживается")
		};
	}

	private static void ValidateBasicConfiguration(JObject config)
	{
		var requiredFields = new[] { "CompanyName", "Host", "PortHttp", "PortHttps", "Database", "Bus" };
		var missingFields = requiredFields.Where(field =>
			string.IsNullOrEmpty(config[field]?.ToString())).ToList();

		if (missingFields.Any())
		{
			throw new InvalidOperationException($"Отсутствуют обязательные поля в конфигурации: {string.Join(", ", missingFields)}");
		}
	}

	private static void ValidateConfiguration(string database, string bus, string protocol, string dataFormat, JObject config)
	{
		// Валидация базы данных
		if (!SupportedDatabases.Contains(database.ToLower()))
		{
			throw new InvalidOperationException($"Неподдерживаемая база данных: {database}. Поддерживаются: {string.Join(", ", SupportedDatabases)}");
		}

		// Валидация шины сообщений
		if (!SupportedMessageBuses.Contains(bus.ToLower()))
		{
			throw new InvalidOperationException($"Неподдерживаемая шина сообщений: {bus}. Поддерживаются: {string.Join(", ", SupportedMessageBuses)}");
		}

		// Проверка наличия настроек для выбранной БД (только для активной)
		if (database == "postgres" && config["PostgresDbSettings"] == null)
		{
			throw new InvalidOperationException("PostgresDbSettings обязательны когда Database = 'postgres'");
		}

		if (database == "mongo" && config["MongoDbSettings"] == null)
		{
			throw new InvalidOperationException("MongoDbSettings обязательны когда Database = 'mongo'");
		}

		// Проверка наличия настроек для выбранной шины
		var busSettingsKey = GetBusSettingsKey(bus);
		if (config[busSettingsKey] == null)
		{
			throw new InvalidOperationException($"{busSettingsKey} обязательны когда Bus = '{bus}'");
		}

		// Валидация протокола
		if (!SupportedProtocols.Contains(protocol.ToLower()))
		{
			throw new InvalidOperationException($"Неподдерживаемый протокол: {protocol}. Поддерживаются: {string.Join(", ", SupportedProtocols)}");
		}

		// Валидация формата данных
		if (!SupportedDataFormats.Contains(dataFormat.ToLower()))
		{
			throw new InvalidOperationException($"Неподдерживаемый формат данных: {dataFormat}. Поддерживаются: {string.Join(", ", SupportedDataFormats)}");
		}
	}

	private static GateConfigurationSettings ParseToStronglyTypedModel(JObject config)
	{
		try
		{
			return config.ToObject<GateConfigurationSettings>()
				?? throw new InvalidOperationException("Не удалось распарсить конфигурацию в типизированную модель");
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Ошибка парсинга конфигурации в типизированную модель: {ex.Message}");
		}
	}

	private static void ValidateGateConfigurationModel(GateConfigurationSettings config)
	{
		var context = new ValidationContext(config);
		var results = new List<ValidationResult>();

		if (!Validator.TryValidateObject(config, context, results, true))
		{
			var errors = string.Join(", ", results.Select(r => r.ErrorMessage));
			throw new InvalidOperationException($"Ошибки валидации типизированной модели: {errors}");
		}
	}

	private static Task ConfigureDatabase(JObject config, WebApplicationBuilder builder, string database)
	{
		// Настраиваем PostgreSQL, если настройки есть в конфигурации
		if (config["PostgresDbSettings"] != null)
		{
			ConfigurePostgreSQL(config, builder);
		}

		// Настраиваем MongoDB, если настройки есть в конфигурации
		if (config["MongoDbSettings"] != null)
		{
			ConfigureMongoDB(config, builder);
		}

		// Проверяем, что основная база настроена
		switch (database.ToLower())
		{
			case "postgres" when config["PostgresDbSettings"] == null:
				throw new InvalidOperationException("PostgresDbSettings обязательны когда Database = 'postgres'");
			case "mongo" when config["MongoDbSettings"] == null:
				throw new InvalidOperationException("MongoDbSettings обязательны когда Database = 'mongo'");
		}
		return Task.CompletedTask;
	}

	private static void ConfigurePostgreSQL(JObject config, WebApplicationBuilder builder)
	{
		var postgresSettings = config["PostgresDbSettings"];
		if (postgresSettings != null)
		{
			var host = postgresSettings["Host"]?.ToString() ?? "localhost";
			var port = postgresSettings["Port"]?.ToString() ?? "5432";
			var username = postgresSettings["Username"]?.ToString() ?? "postgres";
			var password = postgresSettings["Password"]?.ToString() ?? "";
			var database = postgresSettings["Database"]?.ToString() ?? "GatewayDB";

			builder.Configuration["PostgresDbSettings:Host"] = host;
			builder.Configuration["PostgresDbSettings:Port"] = port;
			builder.Configuration["PostgresDbSettings:Username"] = username;
			builder.Configuration["PostgresDbSettings:Password"] = password;
			builder.Configuration["PostgresDbSettings:Database"] = database;

			// Проверяем итоговую строку подключения
			if (!string.IsNullOrEmpty(password))
			{
				var testConnectionString = $"Host={host};Port={port};Username={username};Password={password};Database={database}";
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.ResetColor();
			}
		}
		else
		{
			var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"[{timestamp}] [ERROR] - PostgresDbSettings отсутствует в конфигурации!");
			Console.ResetColor();
		}
	}

	private static void ConfigureMongoDB(JObject config, WebApplicationBuilder builder)
	{
		var mongoSettings = config["MongoDbSettings"];
		if (mongoSettings != null)
		{
			var connectionString = mongoSettings["ConnectionString"]?.ToString() ?? "";
			var databaseName = mongoSettings["DatabaseName"]?.ToString() ?? "GatewayDB";

			// Извлекаем User и Password из ConnectionString, если они не указаны отдельно
			var user = mongoSettings["User"]?.ToString() ?? ExtractUserFromConnectionString(connectionString);
			var password = mongoSettings["Password"]?.ToString() ?? ExtractPasswordFromConnectionString(connectionString);

			// Основные настройки
			builder.Configuration["MongoDbSettings:ConnectionString"] = connectionString;
			builder.Configuration["MongoDbSettings:DatabaseName"] = databaseName;
			builder.Configuration["MongoDbSettings:User"] = user;
			builder.Configuration["MongoDbSettings:Password"] = password;

			// Коллекции
			var collections = mongoSettings["Collections"];
			if (collections != null)
			{
				builder.Configuration["MongoDbSettings:Collections:OutboxCollection"] = collections["OutboxCollection"]?.ToString() ?? "OutboxMessages";
				builder.Configuration["MongoDbSettings:Collections:IncidentCollection"] = collections["IncidentCollection"]?.ToString() ?? "IncidentEntities";
			}
		}
		else
		{
			var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"[{timestamp}] [ERROR] - MongoDbSettings отсутствует в конфигурации!");
			Console.ResetColor();
		}
	}

	// Добавьте эти вспомогательные методы в класс GateConfiguration
	private static string ExtractUserFromConnectionString(string connectionString)
	{
		if (string.IsNullOrEmpty(connectionString))
			return "";

		try
		{
			var uri = new Uri(connectionString);
			return uri.UserInfo?.Split(':')[0] ?? "";
		}
		catch
		{
			return "";
		}
	}

	private static string ExtractPasswordFromConnectionString(string connectionString)
	{
		if (string.IsNullOrEmpty(connectionString))
			return "";

		try
		{
			var uri = new Uri(connectionString);
			var userInfo = uri.UserInfo?.Split(':');
			return userInfo?.Length > 1 ? userInfo[1] : "";
		}
		catch
		{
			return "";
		}
	}

	private static Task ConfigureMessageBus(JObject config, WebApplicationBuilder builder, string bus)
	{
		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

		// 1. RabbitMQ
		if (config["RabbitMqSettings"] != null)
		{
			ConfigureRabbitMQ(config, builder);
		}
		else
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] RabbitMqSettings отсутствуют в конфигурации");
		}

		// 2. ActiveMQ
		if (config["ActiveMqSettings"] != null)
		{
			ConfigureActiveMQ(config, builder);
		}
		else
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] ActiveMqSettings отсутствуют в конфигурации");
		}

		// 3. Pulsar
		if (config["PulsarSettings"] != null)
		{
			ConfigurePulsar(config, builder);
		}
		else
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] PulsarSettings отсутствуют в конфигурации");
		}

		// 4. Tarantool
		if (config["TarantoolSettings"] != null)
		{
			ConfigureTarantool(config, builder);
		}
		else
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] TarantoolSettings отсутствуют в конфигурации");
		}

		// 5. KafkaStreams
		if (config["KafkaStreamsSettings"] != null)
		{
			ConfigureKafkaStreams(config, builder);
		}
		else
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] KafkaStreamsSettings отсутствуют в конфигурации");
		}

		return Task.CompletedTask;
	}

	private static void ConfigureRabbitMQ(JObject config, WebApplicationBuilder builder)
	{

		var rabbitSettings = config["RabbitMqSettings"];
		if (rabbitSettings != null)
		{
			builder.Configuration["RabbitMqSettings:InstanceNetworkGateId"] = rabbitSettings["InstanceNetworkGateId"]?.ToString() ?? "";
			builder.Configuration["RabbitMqSettings:TypeToRun"] = rabbitSettings["TypeToRun"]?.ToString() ?? "RabbitMQ";
			builder.Configuration["RabbitMqSettings:HostName"] = rabbitSettings["HostName"]?.ToString() ?? "localhost";
			builder.Configuration["RabbitMqSettings:Port"] = rabbitSettings["Port"]?.ToString() ?? "5672";
			builder.Configuration["RabbitMqSettings:UserName"] = rabbitSettings["UserName"]?.ToString() ?? "guest";
			builder.Configuration["RabbitMqSettings:Password"] = rabbitSettings["Password"]?.ToString() ?? "guest";
			builder.Configuration["RabbitMqSettings:VirtualHost"] = rabbitSettings["VirtualHost"]?.ToString() ?? "/";
			builder.Configuration["RabbitMqSettings:InputChannel"] = _inPushChannel;
			builder.Configuration["RabbitMqSettings:OutputChannel"] = _outPullChannel;
			builder.Configuration["RabbitMqSettings:Heartbeat"] = rabbitSettings["Heartbeat"]?.ToString() ?? "60";
		}
	}

	private static void ConfigureActiveMQ(JObject config, WebApplicationBuilder builder)
	{
		var activeMqSettings = config["ActiveMqSettings"];
		if (activeMqSettings != null)
		{
			builder.Configuration["ActiveMqSettings:InstanceNetworkGateId"] = activeMqSettings["InstanceNetworkGateId"]?.ToString() ?? "";
			builder.Configuration["ActiveMqSettings:TypeToRun"] = activeMqSettings["TypeToRun"]?.ToString() ?? "ActiveMQ";
			builder.Configuration["ActiveMqSettings:BrokerUri"] = activeMqSettings["BrokerUri"]?.ToString() ?? "";
			builder.Configuration["ActiveMqSettings:InputChannel"] = _inPushChannel;
			builder.Configuration["ActiveMqSettings:OutputChannel"] = _outPullChannel;
		}
	}

	/// <summary>
	/// Настройка Apache Pulsar
	/// </summary>
	private static void ConfigurePulsar(JObject config, WebApplicationBuilder builder)
	{
		var pulsarSettings = config["PulsarSettings"];
		if (pulsarSettings != null)
		{
			builder.Configuration["PulsarSettings:InstanceNetworkGateId"] = pulsarSettings["InstanceNetworkGateId"]?.ToString() ?? "";
			builder.Configuration["PulsarSettings:TypeToRun"] = pulsarSettings["TypeToRun"]?.ToString() ?? "Pulsar";
			builder.Configuration["PulsarSettings:ServiceUrl"] = pulsarSettings["ServiceUrl"]?.ToString() ?? "pulsar://localhost:6650";
			builder.Configuration["PulsarSettings:Tenant"] = pulsarSettings["Tenant"]?.ToString() ?? "public";
			builder.Configuration["PulsarSettings:Namespace"] = pulsarSettings["Namespace"]?.ToString() ?? "default";
			builder.Configuration["PulsarSettings:SubscriptionName"] = pulsarSettings["SubscriptionName"]?.ToString() ?? "default-subscription";
			builder.Configuration["PulsarSettings:SubscriptionType"] = pulsarSettings["SubscriptionType"]?.ToString() ?? "Exclusive";
			builder.Configuration["PulsarSettings:ConnectionTimeoutSeconds"] = pulsarSettings["ConnectionTimeoutSeconds"]?.ToString() ?? "15";
			builder.Configuration["PulsarSettings:MaxReconnectAttempts"] = pulsarSettings["MaxReconnectAttempts"]?.ToString() ?? "3";
			builder.Configuration["PulsarSettings:ReconnectIntervalSeconds"] = pulsarSettings["ReconnectIntervalSeconds"]?.ToString() ?? "5";
			builder.Configuration["PulsarSettings:EnableCompression"] = pulsarSettings["EnableCompression"]?.ToString() ?? "false";
			builder.Configuration["PulsarSettings:CompressionType"] = pulsarSettings["CompressionType"]?.ToString() ?? "LZ4";
			builder.Configuration["PulsarSettings:BatchSize"] = pulsarSettings["BatchSize"]?.ToString() ?? "1000";
			builder.Configuration["PulsarSettings:BatchingMaxPublishDelayMs"] = pulsarSettings["BatchingMaxPublishDelayMs"]?.ToString() ?? "10";
			builder.Configuration["PulsarSettings:InputChannel"] = _inPushChannel;
			builder.Configuration["PulsarSettings:OutputChannel"] = _outPullChannel;
		}
	}

	/// <summary>
	/// Настройка Tarantool
	/// </summary>
	private static void ConfigureTarantool(JObject config, WebApplicationBuilder builder)
	{
		var tarantoolSettings = config["TarantoolSettings"];
		if (tarantoolSettings != null)
		{
			builder.Configuration["TarantoolSettings:InstanceNetworkGateId"] = tarantoolSettings["InstanceNetworkGateId"]?.ToString() ?? "";
			builder.Configuration["TarantoolSettings:TypeToRun"] = tarantoolSettings["TypeToRun"]?.ToString() ?? "Tarantool";
			builder.Configuration["TarantoolSettings:Host"] = tarantoolSettings["Host"]?.ToString() ?? "localhost";
			builder.Configuration["TarantoolSettings:Port"] = tarantoolSettings["Port"]?.ToString() ?? "3301";
			builder.Configuration["TarantoolSettings:Username"] = tarantoolSettings["Username"]?.ToString() ?? "";
			builder.Configuration["TarantoolSettings:Password"] = tarantoolSettings["Password"]?.ToString() ?? "";
			builder.Configuration["TarantoolSettings:StreamName"] = tarantoolSettings["StreamName"]?.ToString() ?? "default-stream";
			builder.Configuration["TarantoolSettings:InputChannel"] = _inPushChannel;
			builder.Configuration["TarantoolSettings:OutputChannel"] = _outPullChannel;
		}
	}

	/// <summary>
	/// Настройка Kafka Streams
	/// </summary>
	private static void ConfigureKafkaStreams(JObject config, WebApplicationBuilder builder)
	{
		var kafkaStreamsSettings = config["KafkaStreamsSettings"];
		if (kafkaStreamsSettings != null)
		{
			builder.Configuration["KafkaStreamsSettings:InstanceNetworkGateId"] = kafkaStreamsSettings["InstanceNetworkGateId"]?.ToString() ?? "";
			builder.Configuration["KafkaStreamsSettings:TypeToRun"] = kafkaStreamsSettings["TypeToRun"]?.ToString() ?? "KafkaStreams";
			builder.Configuration["KafkaStreamsSettings:BootstrapServers"] = kafkaStreamsSettings["BootstrapServers"]?.ToString() ?? "localhost:9092";
			builder.Configuration["KafkaStreamsSettings:ApplicationId"] = kafkaStreamsSettings["ApplicationId"]?.ToString() ?? "gateway-app";
			builder.Configuration["KafkaStreamsSettings:ClientId"] = kafkaStreamsSettings["ClientId"]?.ToString() ?? "gateway-client";
			builder.Configuration["KafkaStreamsSettings:GroupId"] = kafkaStreamsSettings["GroupId"]?.ToString() ?? "gateway-group";
			builder.Configuration["KafkaStreamsSettings:AutoOffsetReset"] = kafkaStreamsSettings["AutoOffsetReset"]?.ToString() ?? "earliest";
			builder.Configuration["KafkaStreamsSettings:EnableAutoCommit"] = kafkaStreamsSettings["EnableAutoCommit"]?.ToString() ?? "true";
			builder.Configuration["KafkaStreamsSettings:SessionTimeoutMs"] = kafkaStreamsSettings["SessionTimeoutMs"]?.ToString() ?? "30000";
			builder.Configuration["KafkaStreamsSettings:SecurityProtocol"] = kafkaStreamsSettings["SecurityProtocol"]?.ToString() ?? "PLAINTEXT";
			builder.Configuration["KafkaStreamsSettings:InputChannel"] = _inPushChannel;
			builder.Configuration["KafkaStreamsSettings:OutputChannel"] = _outPullChannel;
		}
	}

	private static void LogDetailedConfiguration(string companyName, string host, int portHttp, int portHttps, bool enableValidation,
	string database, string bus, int cleanupIntervalSeconds, int outboxMessageTtlSeconds, int incidentEntitiesTtlMonths,
	string protocol, string dataFormat, string applicationType, JObject config)
	{
		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.ResetColor();

		Console.ForegroundColor = ConsoleColor.Gray;
		Console.WriteLine($"Время загрузки:        {timestamp}");
		Console.ResetColor();

		Console.ForegroundColor = ConsoleColor.White;
		Console.WriteLine($"Локация инстанса:      {companyName}");
		Console.WriteLine($"Хост http:             {host}:{portHttp}");
		Console.WriteLine($"Хост https:            {host}:{portHttps}");
		Console.ResetColor();

		//Добавляем отображение типа приложения
		Console.ForegroundColor = ConsoleColor.Magenta;
		Console.WriteLine($"Тип приложения:        {applicationType.ToUpper()}");
		Console.ResetColor();

		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine($"Валидация:             {(enableValidation ? "Включена" : "Отключена")}");
		Console.ResetColor();

		Console.ForegroundColor = ConsoleColor.Gray;
		Console.WriteLine($"Интервал очистки:      {cleanupIntervalSeconds} сек");
		Console.WriteLine($"TTL outbox сообщений:  {outboxMessageTtlSeconds} сек");
		Console.WriteLine($"TTL инцидентов:        {incidentEntitiesTtlMonths} мес");
		Console.ResetColor();

		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.WriteLine($"Формат данных:         {dataFormat.ToUpper()}");
		Console.ResetColor();
		// Подробности базы данных
		if (database.Equals("postgres", StringComparison.CurrentCultureIgnoreCase))
		{
			var pgSettings = config["PostgresDbSettings"];
			if (pgSettings != null)
			{
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.WriteLine($"PostgreSQL:            {pgSettings["Host"]}:{pgSettings["Port"]}/{pgSettings["Database"]}");
				Console.WriteLine($"Пользователь:          {pgSettings["Username"]}");
				Console.ResetColor();
			}
		}
		else if (database.Equals("mongo", StringComparison.CurrentCultureIgnoreCase))
		{
			var mongoSettings = config["MongoDbSettings"];
			if (mongoSettings != null)
			{
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.WriteLine($"MongoDB:               {mongoSettings["DatabaseName"]}");
				var host_mongo = ExtractHostFromConnectionString(mongoSettings["ConnectionString"]?.ToString());
				if (!string.IsNullOrEmpty(host_mongo))
				{
					Console.WriteLine($"MongoDB Host:          {host_mongo}");
				}
				Console.ResetColor();
			}
		}
		// Логирование DataOptions
		var dataOptions = config["DataOptions"];
		if (dataOptions != null)
		{
			Console.WriteLine($"Протокол:              {protocol.ToUpper()}");
			Console.ForegroundColor = ConsoleColor.DarkCyan;
			Console.WriteLine($"Server режим:          {dataOptions["server"]} ({dataOptions["serverDetails"]?["host"]}:{dataOptions["serverDetails"]?["port"]})");
			Console.WriteLine($"Client режим:          {dataOptions["client"]} ({dataOptions["clientDetails"]?["host"]}:{dataOptions["clientDetails"]?["port"]})");
			Console.ResetColor();
		}
		// Подробности шины сообщений
		LogBusDetails(bus, config);

		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine();
		Console.ResetColor();
		Console.WriteLine();
	}

	/// <summary>
	/// Логирование деталей настроек шины сообщений
	/// </summary>
	private static void LogBusDetails(string bus, JObject config)
	{
		Console.ForegroundColor = ConsoleColor.DarkYellow;

		switch (bus.ToLower())
		{
			case "rabbit":
				var rabbitSettings = config["RabbitMqSettings"];
				if (rabbitSettings != null)
				{
					Console.WriteLine($"RabbitMQ:              {rabbitSettings["HostName"]}:{rabbitSettings["Port"]}");
					Console.WriteLine($"Push -> Listen:        {config["InputChannel"]} -> {config["OutputChannel"]}");
					Console.WriteLine($"Virtual Host:          {rabbitSettings["VirtualHost"]}");
					Console.WriteLine($"Gate rabbit ID :       {rabbitSettings["InstanceNetworkGateId"]}");
				}
				break;

			case "activemq":
				var activeMqSettings = config["ActiveMqSettings"];
				if (activeMqSettings != null)
				{
					Console.WriteLine($"ActiveMQ:              {activeMqSettings["BrokerUri"]}");
					Console.WriteLine($"Push -> Listen:        {config["InputChannel"]} -> {config["OutputChannel"]}");
					Console.WriteLine($"Gate activemq ID:      {activeMqSettings["InstanceNetworkGateId"]}");

				}
				break;

			case "pulsar":
				var pulsarSettings = config["PulsarSettings"];
				if (pulsarSettings != null)
				{
					Console.WriteLine($"Pulsar:                {pulsarSettings["ServiceUrl"]}");
					Console.WriteLine($"Tenant/Namespace:      {pulsarSettings["Tenant"]}/{pulsarSettings["Namespace"]}");
					Console.WriteLine($"Push -> Listen:        {config["InputChannel"]} -> {config["OutputChannel"]}");
					Console.WriteLine($"Subscription:          {pulsarSettings["SubscriptionName"]} ({pulsarSettings["SubscriptionType"]})");
					Console.WriteLine($"Compression:           {pulsarSettings["EnableCompression"]} ({pulsarSettings["CompressionType"]})");
					Console.WriteLine($"Gate pulsar ID:        {pulsarSettings["InstanceNetworkGateId"]}");
				}
				break;

			case "tarantool":
				var tarantoolSettings = config["TarantoolSettings"];
				if (tarantoolSettings != null)
				{
					Console.WriteLine($"Tarantool:             {tarantoolSettings["Host"]}:{tarantoolSettings["Port"]}");
					Console.WriteLine($"Push -> Listen:        {config["InputChannel"]} -> {config["OutputChannel"]}");
					Console.WriteLine($"Stream:                {tarantoolSettings["StreamName"]}");
					Console.WriteLine($"Gate tarantool ID:     {tarantoolSettings["InstanceNetworkGateId"]}");
				}
				break;

			case "kafkastreams":
				var kafkaStreamsSettings = config["KafkaStreamsSettings"];
				if (kafkaStreamsSettings != null)
				{
					Console.ForegroundColor = ConsoleColor.DarkGreen;
					Console.WriteLine($"Kafka Streams:         {kafkaStreamsSettings["BootstrapServers"]}");
					Console.WriteLine($"Application ID:        {kafkaStreamsSettings["ApplicationId"]}");
					Console.WriteLine($"Push -> Listen:        {config["InputChannel"]} -> {config["OutputChannel"]}");
					Console.WriteLine($"Group ID:              {kafkaStreamsSettings["GroupId"]}");
					Console.WriteLine($"Security:              {kafkaStreamsSettings["SecurityProtocol"]}");
					Console.WriteLine($"Gate kafkastreams ID:  {kafkaStreamsSettings["InstanceNetworkGateId"]}");
				}
				break;

			default:
				Console.WriteLine($"Неизвестная шина:      {bus}");
				break;
		}

		Console.ResetColor();
	}

	private static string ExtractHostFromConnectionString(string connectionString)
	{
		if (string.IsNullOrEmpty(connectionString))
			return "";

		try
		{
			// Простое извлечение хоста из MongoDB connection string
			var uri = new Uri(connectionString);
			return $"{uri.Host}:{uri.Port}";
		}
		catch
		{
			return "";
		}
	}

	private static JObject LoadConfiguration(string configFilePath)
	{
		try
		{
			var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
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

			// Проверка существования файла.
			if (!File.Exists(fullPath))
				throw new FileNotFoundException("Файл конфигурации не найден", fullPath);

			// Загружаем конфигурацию из файла.
			var json = File.ReadAllText(fullPath);
			var parsedConfig = JObject.Parse(json);

			return parsedConfig;
		}
		catch (Exception ex)
		{
			var errorTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
			throw new InvalidOperationException($"[{errorTimestamp}] Ошибка при загрузке конфигурации из файла '{configFilePath}': {ex.Message}");
		}
	}
}
