using lazy_light_requests_gate.buses;
using lazy_light_requests_gate.rabbitqueuesconnections;

namespace lazy_light_requests_gate.messaging
{
	public class MessageBrokerFactory : IMessageBrokerFactory
	{
		private readonly IServiceProvider _serviceProvider;
		private static string _currentBrokerType;

		public MessageBrokerFactory(IServiceProvider serviceProvider, IConfiguration configuration)
		{
			_serviceProvider = serviceProvider;
			if (string.IsNullOrEmpty(_currentBrokerType))
			{
				_currentBrokerType = configuration["MessageBroker"]?.ToString()?.ToLower() ?? "rabbitmq";
			}
		}

		public IMessageBrokerService CreateMessageBroker(string brokerType)
		{
			var type = brokerType?.ToLower() ?? _currentBrokerType;

			return type switch
			{
				"kafka" => _serviceProvider.GetRequiredService<KafkaService>(),
				"rabbitmq" => _serviceProvider.GetRequiredService<RabbitMqService>(),
				_ => throw new ArgumentException($"Unsupported message broker type: {brokerType}")
			};
		}

		public void SetDefaultBrokerType(string brokerType)
		{
			_currentBrokerType = brokerType?.ToLower() ?? "rabbitmq";
		}

		public string GetCurrentBrokerType()
		{
			return _currentBrokerType;
		}
	}
}

