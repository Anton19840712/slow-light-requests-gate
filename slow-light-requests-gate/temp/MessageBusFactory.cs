using infrastructure.messaging;
using lazy_light_requests_gate.buses;

namespace lazy_light_requests_gate.temp
{
	public class MessageBusFactory : IMessageBusFactory
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger<MessageBusFactory> _logger;

		public MessageBusFactory(IServiceProvider serviceProvider, ILogger<MessageBusFactory> logger)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
		}

		public IMessageBusService Create(MessageBusBaseSettings config)
		{
			return config.TypeToRun switch
			{
				MessageBusType.ActiveMq => ActivatorUtilities.CreateInstance<ActiveMqService>(_serviceProvider),
				MessageBusType.RabbitMq => ActivatorUtilities.CreateInstance<RabbitMqService>(_serviceProvider),
				MessageBusType.KafkaStreams => ActivatorUtilities.CreateInstance<KafkaStreamsService>(_serviceProvider),
				_ => throw new NotSupportedException($"Неизвестный тип шины: {config.TypeToRun}")
			};
		}
	}
}
