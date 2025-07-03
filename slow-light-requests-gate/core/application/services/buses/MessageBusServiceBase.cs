using lazy_light_requests_gate.core.application.interfaces.buses;

namespace lazy_light_requests_gate.core.application.services.buses
{
	/// <summary>
	/// Базовый класс для всех сервисов шин сообщений
	/// </summary>
	public abstract class MessageBusServiceBase : IMessageBusService
	{
		protected readonly ILogger _logger;
		protected readonly string _busType;

		protected MessageBusServiceBase(ILogger logger, string busType)
		{
			_logger = logger;
			_busType = busType;
		}

		public abstract Task PublishMessageAsync(string queueName, string routingKey, string message);
		public abstract Task StartListeningAsync(string queueName, CancellationToken cancellationToken);
		public abstract Task<bool> TestConnectionAsync();

		public string GetBusType() => _busType;
	}
}
