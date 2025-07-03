using lazy_light_requests_gate.core.application.interfaces.buses;
using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.core.application.services.buses;
using lazy_light_requests_gate.core.application.services.messageprocessing;
using lazy_light_requests_gate.infrastructure.configuration;
using Serilog;

Console.Title = "slow & light rest http protocol dynamic gate";
var instanceId = $"{Environment.MachineName}_{Guid.NewGuid()}";
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"\nЗапускается экземпляр DynamicGate ID: {instanceId}\n");
Console.ResetColor();

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);
LoggingConfiguration.ConfigureLogging(builder);

// СНАЧАЛА: Конфигурация динамического шлюза, шин, баз данных
var (httpUrl, httpsUrl) = await GateConfiguration.ConfigureDynamicGateAsync(args, builder);

// ПОТОМ: Регистрируем сервисы УСЛОВНО (ПОСЛЕ обновления конфигурации)
ConfigureServices(builder, instanceId);

var app = builder.Build();

try
{
	// УСЛОВНАЯ инициализация только выбранных сервисов
	await InitializeSelectedServicesAsync(app);

	// Применяем настройки приложения:
	ConfigureApp(app, httpUrl, httpsUrl);

	// УСЛОВНАЯ настройка Dapper только для PostgreSQL
	var selectedDatabase = app.Configuration["Database"]?.ToLower();
	if (selectedDatabase == "postgres")
	{
		Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
	}

	await app.RunAsync();
}
catch (Exception ex)
{
	Log.Fatal(ex, "Критическая ошибка при запуске приложения");
	throw;
}
finally
{
	Log.CloseAndFlush();
}

static void ConfigureServices(WebApplicationBuilder builder, string instanceId)
{
	var configuration = builder.Configuration;
	var services = builder.Services;

	// Получаем выбранные в конфигурации сервисы
	var selectedDatabase = configuration["Database"]?.ToLower();
	var selectedBus = configuration["Bus"]?.ToLower();

	var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
	Console.WriteLine($"[{timestamp}] [CONFIG] Регистрация сервисов для Database='{selectedDatabase}', Bus='{selectedBus}'");

	// Базовые сервисы (всегда нужны)
	services.AddSingleton(instanceId);
	services.AddControllers();
	services.AddEndpointsApiExplorer();
	services.AddSwaggerGen();

	// Общие сервисы (всегда нужны)
	services.AddCommonServices();
	services.AddHttpServices();
	services.AddHeadersServices();

	// === УСЛОВНАЯ РЕГИСТРАЦИЯ БАЗ ДАННЫХ ===

	Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется MongoDB");
	services.AddMongoDbServices(configuration);
	services.AddMongoDbRepositoriesServices(configuration);
	// Регистрируем сервисы обработки сообщений для MongoDB
	services.AddMessageServingServices(configuration);

	Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется PostgreSQL");
	services.AddPostgresDbServices(configuration);
	services.AddPostgresDbRepositoriesServices(configuration);
	// Регистрируем сервисы обработки сообщений для PostgreSQL
	services.AddMessageServingServices(configuration);


	// === УСЛОВНАЯ РЕГИСТРАЦИЯ ШИН СООБЩЕНИЙ ===
	switch (selectedBus)
	{
		case "rabbit":
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется RabbitMQ");
			Console.WriteLine("=== ДОБАВЛЯЕМ RABBITMQ СЕРВИСЫ ===");
			Console.WriteLine($"RabbitMQ Host из конфигурации: {configuration["RabbitMqSettings:HostName"]}");
			Console.WriteLine($"RabbitMQ Port из конфигурации: {configuration["RabbitMqSettings:Port"]}");
			Console.WriteLine($"RabbitMQ User из конфигурации: {configuration["RabbitMqSettings:UserName"]}");
			Console.WriteLine($"RabbitMQ VHost из конфигурации: {configuration["RabbitMqSettings:VirtualHost"]}");
			Console.WriteLine("================================");
			services.AddRabbitMqServices(configuration);
			break;

		case "activemq":
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется ActiveMQ");
			services.AddSingleton<IActiveMqService>(sp =>
			{
				var serviceUrl = configuration["ActiveMqSettings:BrokerUri"] ?? "tcp://localhost:61616";
				var logger = sp.GetRequiredService<ILogger<ActiveMqService>>();
				Console.WriteLine($"[{timestamp}] [CONFIG] ActiveMQ BrokerUri: {serviceUrl}");
				return new ActiveMqService(serviceUrl, logger);
			});
			break;

		case "kafka":
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется Kafka");
			services.AddSingleton<IKafkaStreamsService, KafkaStreamsService>();
			break;

		case "pulsar":
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется Pulsar");
			services.AddSingleton<IPulsarService, PulsarService>();
			break;

		case "tarantool":
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется Tarantool");
			services.AddSingleton<ITarantoolService, TarantoolService>();
			break;

		default:
			throw new InvalidOperationException($"Неподдерживаемая шина сообщений: {selectedBus}. Поддерживаются: rabbit, activemq, kafka, pulsar, tarantool");
	}

	// Общие сервисы шин сообщений и hosted services (могут зависеть от выбранной БД)
	Console.WriteLine($"[{timestamp}] [CONFIG] Регистрация дополнительных сервисов для {selectedDatabase}/{selectedBus}");

	// Hosted Services - регистрируем только для активных сервисов
	if (selectedDatabase == "postgres" || selectedDatabase == "mongo")
	{
		services.AddHostedServices(configuration);
	}

	// Регистрируем существующие сервисы
	services.AddScoped<IMessageProcessingServiceFactory, MessageProcessingServiceFactory>();
	services.AddSingleton<IMessageBusServiceFactory, MessageBusServiceFactory>();
	services.AddAuthentication();
	services.AddAuthorization();

	Console.WriteLine($"[{timestamp}] [CONFIG] Регистрация сервисов завершена");
}

static async Task InitializeSelectedServicesAsync(WebApplication app)
{
	var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
	var selectedDatabase = app.Configuration["Database"]?.ToLower();
	var selectedBus = app.Configuration["Bus"]?.ToLower();

	Console.WriteLine($"[{timestamp}] [INIT] Инициализация выбранных сервисов...");
	Console.WriteLine($"[{timestamp}] [INIT] Database: {selectedDatabase}, Bus: {selectedBus}");

	// === ИНИЦИАЛИЗАЦИЯ БАЗЫ ДАННЫХ ===
	switch (selectedDatabase)
	{
		//	если база данных не была до этого создана, при переключении на нее будут ли вызываться 
		//	await PostgresDbConfiguration.EnsureDatabaseInitializedAsync(app.Configuration);
		//	await PostgresDbConfiguration.DiagnoseDatabaseAsync(app.Configuration);

		case "postgres":
			Console.WriteLine($"[{timestamp}] [INIT] Инициализация PostgreSQL...");
			await PostgresDbConfiguration.EnsureDatabaseInitializedAsync(app.Configuration);
			await PostgresDbConfiguration.DiagnoseDatabaseAsync(app.Configuration);
			Console.WriteLine($"[{timestamp}] [INIT] PostgreSQL инициализирован успешно");
			break;

		case "mongo":
			Console.WriteLine($"[{timestamp}] [INIT] Инициализация MongoDB...");
			// TODO: Добавить инициализацию MongoDB если потребуется
			Console.WriteLine($"[{timestamp}] [INIT] MongoDB готов к работе");
			break;

		default:
			throw new InvalidOperationException($"Неподдерживаемая база данных: {selectedDatabase}");
	}

	// === ИНИЦИАЛИЗАЦИЯ ШИНЫ СООБЩЕНИЙ ===
	switch (selectedBus)
	{
		case "rabbit":
			Console.WriteLine($"[{timestamp}] [INIT] Инициализация RabbitMQ...");
			// Инициализируем ТОЛЬКО RabbitMQ, без тестирования других шин
			await InitializeRabbitMqOnlyAsync(app.Services, app.Configuration, CancellationToken.None);
			Console.WriteLine($"[{timestamp}] [INIT] RabbitMQ инициализирован успешно");
			break;

		case "activemq":
			Console.WriteLine($"[{timestamp}] [INIT] Инициализация ActiveMQ...");
			// TODO: Добавить специфичную инициализацию ActiveMQ
			await InitializeActiveMqOnlyAsync(app.Services, app.Configuration, CancellationToken.None);
			Console.WriteLine($"[{timestamp}] [INIT] ActiveMQ инициализирован успешно");
			break;

		case "kafka":
			Console.WriteLine($"[{timestamp}] [INIT] Инициализация Kafka...");
			// TODO: Добавить специфичную инициализацию Kafka
			await InitializeKafkaOnlyAsync(app.Services, app.Configuration, CancellationToken.None);
			Console.WriteLine($"[{timestamp}] [INIT] Kafka инициализирован успешно");
			break;

		case "pulsar":
			Console.WriteLine($"[{timestamp}] [INIT] Инициализация Pulsar...");
			// TODO: Добавить специфичную инициализацию Pulsar
			await InitializePulsarOnlyAsync(app.Services, app.Configuration, CancellationToken.None);
			Console.WriteLine($"[{timestamp}] [INIT] Pulsar инициализирован успешно");
			break;

		case "tarantool":
			Console.WriteLine($"[{timestamp}] [INIT] Инициализация Tarantool...");
			// TODO: Добавить специфичную инициализацию Tarantool
			await InitializeTarantoolOnlyAsync(app.Services, app.Configuration, CancellationToken.None);
			Console.WriteLine($"[{timestamp}] [INIT] Tarantool инициализирован успешно");
			break;

		default:
			throw new InvalidOperationException($"Неподдерживаемая шина сообщений: {selectedBus}");
	}

	Console.WriteLine($"[{timestamp}] [INIT] Инициализация всех выбранных сервисов завершена успешно");
}

// ========================================================================
// СПЕЦИФИЧНЫЕ МЕТОДЫ ИНИЦИАЛИЗАЦИИ ШИН (БЕЗ ТЕСТИРОВАНИЯ АЛЬТЕРНАТИВНЫХ)
// ========================================================================

static async Task InitializeRabbitMqOnlyAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
{
	var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
	Console.WriteLine($"[{timestamp}] [INIT] Инициализация ТОЛЬКО RabbitMQ (без тестирования других шин)...");

	try
	{
		// Получаем RabbitMQ сервис и тестируем только его
		var rabbitService = services.GetRequiredService<IRabbitMqBusService>();

		// Тестируем подключение к RabbitMQ
		await rabbitService.TestConnectionAsync();
		Console.WriteLine($"[{timestamp}] [SUCCESS] RabbitMQ подключение протестировано успешно");

		// Запускаем слушатель RabbitMQ
		var queueName = configuration["RabbitMqSettings:ListenQueueName"];
		if (!string.IsNullOrEmpty(queueName))
		{
			await rabbitService.StartListeningAsync(queueName, cancellationToken);
			Console.WriteLine($"[{timestamp}] [SUCCESS] RabbitMQ слушатель запущен для очереди: {queueName}");
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"[{timestamp}] [ERROR] Ошибка инициализации RabbitMQ: {ex.Message}");
		throw;
	}
}

static async Task InitializeActiveMqOnlyAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
{
	var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
	Console.WriteLine($"[{timestamp}] [INIT] Инициализация ТОЛЬКО ActiveMQ...");

	try
	{
		var activeMqService = services.GetRequiredService<IActiveMqService>();
		Console.WriteLine($"[{timestamp}] [INIT] ActiveMQ сервис получен");

		// Тестируем подключение
		var connectionTest = await activeMqService.TestConnectionAsync();
		if (connectionTest)
		{
			Console.WriteLine($"[{timestamp}] [SUCCESS] ActiveMQ подключение протестировано успешно");

			// Запускаем слушатель
			var queueName = configuration["ActiveMqSettings:ListenQueueName"];
			if (!string.IsNullOrEmpty(queueName))
			{
				await activeMqService.StartListeningAsync(queueName, cancellationToken);
				Console.WriteLine($"[{timestamp}] [SUCCESS] ActiveMQ слушатель запущен для очереди: {queueName}");
			}
		}
		else
		{
			Console.WriteLine($"[{timestamp}] [WARNING] ActiveMQ тест подключения неудачен");
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"[{timestamp}] [ERROR] Ошибка инициализации ActiveMQ: {ex.Message}");
		throw;
	}
}

static async Task InitializeKafkaOnlyAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
{
	var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
	Console.WriteLine($"[{timestamp}] [INIT] Инициализация ТОЛЬКО Kafka...");

	try
	{
		var kafkaService = services.GetService<IKafkaStreamsService>();
		if (kafkaService != null)
		{
			Console.WriteLine($"[{timestamp}] [INIT] Kafka сервис получен");

			// Тестируем подключение
			var connectionTest = await kafkaService.TestConnectionAsync();
			if (connectionTest)
			{
				Console.WriteLine($"[{timestamp}] [SUCCESS] Kafka подключение протестировано успешно");

				// Запускаем Kafka Streams
				await kafkaService.StartAsync(cancellationToken);
				Console.WriteLine($"[{timestamp}] [SUCCESS] Kafka Streams запущен");

				// Запускаем слушатель
				var topicName = configuration["KafkaStreamsSettings:InputTopic"];
				if (!string.IsNullOrEmpty(topicName))
				{
					await kafkaService.StartListeningAsync(topicName, cancellationToken);
					Console.WriteLine($"[{timestamp}] [SUCCESS] Kafka слушатель запущен для топика: {topicName}");
				}
			}
			else
			{
				Console.WriteLine($"[{timestamp}] [WARNING] Kafka тест подключения неудачен");
			}
		}
		else
		{
			Console.WriteLine($"[{timestamp}] [WARNING] KafkaStreamsService не зарегистрирован");
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"[{timestamp}] [ERROR] Ошибка инициализации Kafka: {ex.Message}");
		throw;
	}
}

static async Task InitializePulsarOnlyAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
{
	var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
	Console.WriteLine($"[{timestamp}] [INIT] Инициализация ТОЛЬКО Pulsar...");

	try
	{
		var pulsarService = services.GetService<IPulsarService>();
		if (pulsarService != null)
		{
			Console.WriteLine($"[{timestamp}] [INIT] Pulsar сервис получен");

			// Тестируем подключение
			var connectionTest = await pulsarService.TestConnectionAsync();
			if (connectionTest)
			{
				Console.WriteLine($"[{timestamp}] [SUCCESS] Pulsar подключение протестировано успешно");

				// Запускаем слушатель
				var topicName = configuration["PulsarSettings:InputTopic"];
				if (!string.IsNullOrEmpty(topicName))
				{
					await pulsarService.StartListeningAsync(topicName, cancellationToken);
					Console.WriteLine($"[{timestamp}] [SUCCESS] Pulsar слушатель запущен для топика: {topicName}");
				}
			}
			else
			{
				Console.WriteLine($"[{timestamp}] [WARNING] Pulsar тест подключения неудачен");
			}
		}
		else
		{
			Console.WriteLine($"[{timestamp}] [WARNING] PulsarService не зарегистрирован");
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"[{timestamp}] [ERROR] Ошибка инициализации Pulsar: {ex.Message}");
		throw;
	}
}

static async Task InitializeTarantoolOnlyAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
{
	var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
	Console.WriteLine($"[{timestamp}] [INIT] Инициализация ТОЛЬКО Tarantool...");

	try
	{
		var tarantoolService = services.GetService<ITarantoolService>();
		if (tarantoolService != null)
		{
			Console.WriteLine($"[{timestamp}] [INIT] Tarantool сервис получен");

			// Тестируем подключение
			var connectionTest = await tarantoolService.TestConnectionAsync();
			if (connectionTest)
			{
				Console.WriteLine($"[{timestamp}] [SUCCESS] Tarantool подключение протестировано успешно");

				// Запускаем слушатель
				var spaceName = configuration["TarantoolSettings:OutputSpace"];
				if (!string.IsNullOrEmpty(spaceName))
				{
					await tarantoolService.StartListeningAsync(spaceName, cancellationToken);
					Console.WriteLine($"[{timestamp}] [SUCCESS] Tarantool слушатель запущен для пространства: {spaceName}");
				}
			}
			else
			{
				Console.WriteLine($"[{timestamp}] [WARNING] Tarantool тест подключения неудачен");
			}
		}
		else
		{
			Console.WriteLine($"[{timestamp}] [WARNING] TarantoolService не зарегистрирован");
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"[{timestamp}] [ERROR] Ошибка инициализации Tarantool: {ex.Message}");
		throw;
	}
}

static void ConfigureApp(WebApplication app, string httpUrl, string httpsUrl)
{
	app.Urls.Add(httpUrl);
	app.Urls.Add(httpsUrl);
	Log.Information($"Middleware: шлюз работает на адресах: {httpUrl} и {httpsUrl}");

	app.UseSerilogRequestLogging();
	app.UseCors(cors => cors
		.AllowAnyOrigin()
		.AllowAnyMethod()
		.AllowAnyHeader());

	app.UseAuthentication();
	app.UseSwagger();
	app.UseSwaggerUI(c =>
	{
		c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dynamic Gate API v1");
		c.RoutePrefix = "/swagger"; // чтобы Swagger открывался по адресу "/"
	});

	app.UseAuthorization();
	app.MapControllers();

	// Диагностика маршрутов
	var endpoints = app.Services.GetRequiredService<EndpointDataSource>();
	foreach (var endpoint in endpoints.Endpoints)
	{
		if (endpoint is RouteEndpoint routeEndpoint)
		{
			Console.WriteLine($"Найден маршрут: {routeEndpoint.RoutePattern.RawText}");
		}
	}
}