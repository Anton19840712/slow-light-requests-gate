namespace lazy_light_requests_gate.core.application.interfaces.buses
{
	/// <summary>
	/// Интерфейс для работы с шинами сообщений
	/// </summary>
	public interface IMessageBusService
	{
		Task PublishMessageAsync(string queueName, string routingKey, string message);
		Task StartListeningAsync(string queueName, CancellationToken cancellationToken);
		Task<bool> TestConnectionAsync();
		string GetBusType();
	}
}
