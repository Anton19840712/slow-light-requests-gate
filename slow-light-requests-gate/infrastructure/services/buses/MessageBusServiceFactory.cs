using lazy_light_requests_gate.core.application.interfaces.buses;
using lazy_light_requests_gate.core.domain.settings.buses;
using System.Collections.Concurrent;

namespace lazy_light_requests_gate.infrastructure.services.buses;

/// <summary>
/// Фабрика для создания и управления сервисами шин сообщений
/// </summary>
public class MessageBusServiceFactory : IMessageBusServiceFactory
{
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly ILogger<MessageBusServiceFactory> _logger;
	private readonly IConfiguration _configuration;
	private string _currentBusType;

	// Кэш для хранения динамически созданных экземпляров
	private readonly ConcurrentDictionary<string, IMessageBusService> _dynamicInstances;
	private readonly object _lockObject = new object();

	public MessageBusServiceFactory(
		IServiceScopeFactory serviceScopeFactory,
		ILogger<MessageBusServiceFactory> logger,
		IConfiguration configuration)
	{
		_serviceScopeFactory = serviceScopeFactory;
		_logger = logger;
		_configuration = configuration;
		_dynamicInstances = new ConcurrentDictionary<string, IMessageBusService>();

		// Устанавливаем тип шины по умолчанию из начальной конфигурации
		_currentBusType = _configuration["Bus"]?.ToLower() ?? "rabbit";

		_logger.LogInformation("MessageBusServiceFactory initialized with default bus type: {BusType}", _currentBusType);
	}

	public IMessageBusService CreateMessageBusService(string busType)
	{
		var normalizedBusType = busType?.ToLower() ?? throw new ArgumentNullException(nameof(busType));

		// Сначала проверяем, есть ли динамический экземпляр
		if (_dynamicInstances.TryGetValue(normalizedBusType, out var dynamicInstance))
		{
			_logger.LogDebug("Using dynamic instance for bus type: {BusType}", normalizedBusType);
			return dynamicInstance;
		}

		// Если нет динамического экземпляра, создаем стандартный
		return CreateStandardMessageBusService(normalizedBusType);
	}

	private IMessageBusService CreateStandardMessageBusService(string normalizedBusType)
	{
		_logger.LogDebug("Creating standard message bus service for type: {BusType}", normalizedBusType);

		// Создаем scope БЕЗ using - он будет управляться вручную
		var scope = _serviceScopeFactory.CreateScope();
		var serviceProvider = scope.ServiceProvider;

		try
		{
			return normalizedBusType switch
			{
				"rabbit" => serviceProvider.GetRequiredService<IRabbitMqBusService>(),
				"activemq" => serviceProvider.GetRequiredService<IActiveMqService>(),
				"pulsar" => CreatePulsarService(serviceProvider),
				"tarantool" => CreateTarantoolService(serviceProvider),
				"kafkastreams" => CreateKafkaStreamsService(serviceProvider),
				_ => throw new NotSupportedException($"Bus type '{normalizedBusType}' is not supported. Supported types: rabbit, activemq, pulsar, tarantool, kafkastreams")
			};
		}
		finally
		{
			// НЕ вызываем scope.Dispose() для Pulsar и Tarantool, так как они создаются отдельно
			if (normalizedBusType != "pulsar" && normalizedBusType != "tarantool" && normalizedBusType != "kafkastreams")
			{
				scope.Dispose();
			}
		}
	}

	// Новые методы для динамического переподключения

	public async Task<IMessageBusService> CreateDynamicMessageBusServiceAsync(
		RabbitMqSettings rabbitSettings = null,
		ActiveMqSettings activeMqSettings = null,
		KafkaStreamsSettings kafkaSettings = null,
		PulsarSettings pulsarSettings = null)
	{
		_logger.LogInformation("Creating dynamic message bus service");

		if (rabbitSettings != null)
		{
			return await CreateDynamicRabbitMqService(rabbitSettings);
		}
		if (activeMqSettings != null)
		{
			return await CreateDynamicActiveMqService(activeMqSettings);
		}
		if (kafkaSettings != null)
		{
			return await CreateDynamicKafkaService(kafkaSettings);
		}
		if (pulsarSettings != null)
		{
			return await CreateDynamicPulsarService(pulsarSettings);
		}

		throw new ArgumentException("No settings provided for dynamic reconnection");
	}

	public async Task<bool> TestDynamicConnectionAsync(
		RabbitMqSettings rabbitSettings = null,
		ActiveMqSettings activeMqSettings = null,
		KafkaStreamsSettings kafkaSettings = null,
		PulsarSettings pulsarSettings = null)
	{
		try
		{
			var dynamicService = await CreateDynamicMessageBusServiceAsync(rabbitSettings, activeMqSettings, kafkaSettings, pulsarSettings);
			return await dynamicService.TestConnectionAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error testing dynamic connection");
			return false;
		}
	}

	public void SetDynamicBusInstance(string busType, IMessageBusService busService)
	{
		busType = busType.ToLower();

		lock (_lockObject)
		{
			// Диспозим предыдущий экземпляр, если он есть
			if (_dynamicInstances.TryGetValue(busType, out var oldInstance))
			{
				if (oldInstance is IDisposable disposable)
				{
					disposable.Dispose();
				}
			}

			_dynamicInstances[busType] = busService;
			_logger.LogInformation("Dynamic bus instance set for type: {BusType}", busType);
		}
	}

	public void RestoreDefaultBusConfiguration(string busType)
	{
		busType = busType.ToLower();

		lock (_lockObject)
		{
			// Удаляем динамический экземпляр
			if (_dynamicInstances.TryRemove(busType, out var dynamicInstance))
			{
				if (dynamicInstance is IDisposable disposable)
				{
					disposable.Dispose();
				}
				_logger.LogInformation("Dynamic instance removed for bus type: {BusType}", busType);
			}
		}
	}

	// Приватные методы для создания динамических сервисов
	private async Task<IMessageBusService> CreateDynamicRabbitMqService(RabbitMqSettings settings)
	{
		// Создаем RabbitMQ ConnectionFactory напрямую из настроек
		var factory = new RabbitMQ.Client.ConnectionFactory()
		{
			HostName = settings.HostName,
			Port = settings.Port,
			UserName = settings.UserName,
			Password = settings.Password,
			VirtualHost = settings.VirtualHost,
			RequestedHeartbeat = TimeSpan.FromSeconds(int.Parse(settings.Heartbeat))
		};

		using var scope = _serviceScopeFactory.CreateScope();
		var logger = scope.ServiceProvider.GetService<ILogger<RabbitMqBusService>>();

		// ИСПРАВЛЕНО: правильный порядок параметров (logger, factory)
		return await Task.FromResult(new RabbitMqBusService(factory, logger));
	}

	private async Task<IMessageBusService> CreateDynamicActiveMqService(ActiveMqSettings settings)
	{
		using var scope = _serviceScopeFactory.CreateScope();
		var logger = scope.ServiceProvider.GetService<ILogger<ActiveMqService>>();

		return await Task.FromResult(new ActiveMqService(settings.BrokerUri, logger));
	}

	private async Task<IMessageBusService> CreateDynamicKafkaService(KafkaStreamsSettings settings)
	{
		// Создаем временную конфигурацию напрямую из настроек
		var tempConfig = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string>
			{
				["KafkaStreamsSettings:BootstrapServers"] = settings.BootstrapServers,
				["KafkaStreamsSettings:ApplicationId"] = settings.ApplicationId,
				["KafkaStreamsSettings:ClientId"] = settings.ClientId,
				["KafkaStreamsSettings:InputTopic"] = settings.InputChannel,
				["KafkaStreamsSettings:OutputTopic"] = settings.OutputChannel,
				["KafkaStreamsSettings:GroupId"] = settings.GroupId,
				["KafkaStreamsSettings:AutoOffsetReset"] = settings.AutoOffsetReset,
				["KafkaStreamsSettings:EnableAutoCommit"] = settings.EnableAutoCommit.ToString(),
				["KafkaStreamsSettings:SessionTimeoutMs"] = settings.SessionTimeoutMs.ToString(),
				["KafkaStreamsSettings:SecurityProtocol"] = settings.SecurityProtocol
			})
			.Build();

		using var scope = _serviceScopeFactory.CreateScope();
		var logger = scope.ServiceProvider.GetService<ILogger<KafkaStreamsService>>();

		return await Task.FromResult(new KafkaStreamsService(tempConfig, logger));
	}

	private async Task<IMessageBusService> CreateDynamicPulsarService(PulsarSettings settings)
	{
		// Create a temporary configuration from PulsarSettings
		var tempConfig = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string>
			{
				["PulsarSettings:ServiceUrl"] = settings.ServiceUrl,
				["PulsarSettings:Tenant"] = settings.Tenant,
				["PulsarSettings:Namespace"] = settings.Namespace,
				["PulsarSettings:InputTopic"] = settings.InputChannel,
				["PulsarSettings:OutputTopic"] = settings.OutputChannel,
				["PulsarSettings:SubscriptionName"] = settings.SubscriptionName
			})
			.Build();

		using var scope = _serviceScopeFactory.CreateScope();
		var logger = scope.ServiceProvider.GetService<ILogger<PulsarService>>();

		// Pass the temporary configuration instead of settings
		return await Task.FromResult(new PulsarService(tempConfig, logger));
	}

	// Существующие методы

	private IMessageBusService CreatePulsarService(IServiceProvider serviceProvider)
	{
		// Просто создаем PulsarService - он сам прочитает конфигурацию
		var logger = serviceProvider.GetService<ILogger<PulsarService>>();
		var configuration = serviceProvider.GetRequiredService<IConfiguration>();

		return new PulsarService(configuration, logger);
	}

	private IMessageBusService CreateTarantoolService(IServiceProvider serviceProvider)
	{
		var logger = serviceProvider.GetService<ILogger<TarantoolService>>();
		var configuration = serviceProvider.GetRequiredService<IConfiguration>();

		return new TarantoolService(configuration, logger);
	}

	private IMessageBusService CreateKafkaStreamsService(IServiceProvider serviceProvider)
	{
		var logger = serviceProvider.GetService<ILogger<KafkaStreamsService>>();
		var configuration = serviceProvider.GetRequiredService<IConfiguration>();

		return new KafkaStreamsService(configuration, logger);
	}

	public void SetDefaultBusType(string busType)
	{
		var normalizedBusType = busType?.ToLower() ?? throw new ArgumentNullException(nameof(busType));

		if (!IsValidBusType(normalizedBusType))
		{
			throw new ArgumentException($"Unsupported bus type: {busType}. Supported types: rabbit, activemq, pulsar, tarantool, kafkastreams");
		}

		var previousBusType = _currentBusType;
		_currentBusType = normalizedBusType;

		_logger.LogInformation("Bus type switched from {PreviousBusType} to {NewBusType}", previousBusType, _currentBusType);
	}

	private static bool IsValidBusType(string busType)
	{
		return busType is "rabbit" or "activemq" or "pulsar" or "tarantool" or "kafkastreams";
	}

	public string GetCurrentBusType()
	{
		return _currentBusType;
	}

	public async Task<bool> TestCurrentBusConnectionAsync()
	{
		try
		{
			var busService = CreateMessageBusService(_currentBusType);
			return await busService.TestConnectionAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error testing connection for current bus type: {BusType}", _currentBusType);
			return false;
		}
	}
}