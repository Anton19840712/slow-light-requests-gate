using lazy_light_requests_gate.buses;
using lazy_light_requests_gate.rabbitqueuesconnections;

namespace lazy_light_requests_gate.messaging
{
	public class MessageBrokerFactory : IMessageBrokerFactory
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger<MessageBrokerFactory> _logger;
		private static string _currentBrokerType;

		public MessageBrokerFactory(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<MessageBrokerFactory> logger)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
			if (string.IsNullOrEmpty(_currentBrokerType))
			{
				_currentBrokerType = configuration["MessageBroker"]?.ToString()?.ToLower() ?? "rabbitmq";
				_logger.LogInformation("Initial message broker type set to: {BrokerType}", _currentBrokerType);
			}
		}

		public IMessageBrokerService CreateMessageBroker(string brokerType)
		{
			var type = brokerType?.ToLower() ?? _currentBrokerType;
			_logger.LogInformation("Creating message broker of type: {BrokerType} (requested: {RequestedType}, current: {CurrentType})",
				type, brokerType, _currentBrokerType);

			IMessageBrokerService service = type switch
			{
				"kafka" => _serviceProvider.GetRequiredService<KafkaService>(),
				"rabbitmq" => _serviceProvider.GetRequiredService<IRabbitMqService>() as IMessageBrokerService,
				_ => throw new ArgumentException($"Unsupported message broker type: {brokerType}")
			};

			_logger.LogInformation("Successfully created {ServiceType} service", service.GetType().Name);
			return service;
		}

		public void SetDefaultBrokerType(string brokerType)
		{
			var oldType = _currentBrokerType;
			_currentBrokerType = brokerType?.ToLower() ?? "rabbitmq";
			_logger.LogInformation("Message broker type changed from {OldType} to {NewType}", oldType, _currentBrokerType);
		}

		public string GetCurrentBrokerType()
		{
			return _currentBrokerType;
		}
	}
}
