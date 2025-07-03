using lazy_light_requests_gate.core.application.interfaces.buses;

namespace lazy_light_requests_gate.core.application.services.buses;

/// <summary>
/// Фабрика для создания и управления сервисами шин сообщений
/// </summary>
public class MessageBusServiceFactory : IMessageBusServiceFactory
{
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly ILogger<MessageBusServiceFactory> _logger;
	private readonly IConfiguration _configuration;
	private string _currentBusType;

	public MessageBusServiceFactory(
		IServiceScopeFactory serviceScopeFactory,
		ILogger<MessageBusServiceFactory> logger,
		IConfiguration configuration)
	{
		_serviceScopeFactory = serviceScopeFactory;
		_logger = logger;
		_configuration = configuration;

		// Устанавливаем тип шины по умолчанию из начальной конфигурации
		_currentBusType = _configuration["Bus"]?.ToLower() ?? "rabbit";

		// здесь неверно логируется
		_logger.LogInformation("MessageBusServiceFactory initialized with default bus type: {BusType}", _currentBusType);
	}

	public IMessageBusService CreateMessageBusService(string busType)
	{
		var normalizedBusType = busType?.ToLower() ?? throw new ArgumentNullException(nameof(busType));

		_logger.LogDebug("Creating message bus service for type: {BusType}", normalizedBusType);

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
				_ => throw new NotSupportedException($"Bus type '{busType}' is not supported. Supported types: rabbit, activemq, pulsar, tarantool, kafka")
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

	private IMessageBusService CreatePulsarService(IServiceProvider serviceProvider)
	{
		// Просто создаем PulsarService - он сам прочитает конфигурацию
		var logger = serviceProvider.GetService<ILogger<PulsarService>>();
		var configuration = serviceProvider.GetRequiredService<IConfiguration>();

		return new PulsarService(configuration, logger);
	}

	private IMessageBusService CreateKafkaService()
	{
		// Если у вас есть Kafka сервис, верните его здесь
		throw new NotSupportedException("Kafka service is not implemented yet");
	}

	private IMessageBusService CreateTarantoolService(IServiceProvider serviceProvider)
	{
		var logger = serviceProvider.GetService<ILogger<TarantoolService>>();
		var configuration = serviceProvider.GetRequiredService<IConfiguration>();

		return new TarantoolService(configuration, logger);
	}
	public void SetDefaultBusType(string busType)
	{
		var normalizedBusType = busType?.ToLower() ?? throw new ArgumentNullException(nameof(busType));

		if (!IsValidBusType(normalizedBusType))
		{
			throw new ArgumentException($"Unsupported bus type: {busType}. Supported types: rabbit, activemq, pulsar, tarantool, kafka");
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

	private IMessageBusService CreateKafkaStreamsService(IServiceProvider serviceProvider)
	{
		var logger = serviceProvider.GetService<ILogger<KafkaStreamsService>>();
		var configuration = serviceProvider.GetRequiredService<IConfiguration>();

		return new KafkaStreamsService(configuration, logger);
	}
}
