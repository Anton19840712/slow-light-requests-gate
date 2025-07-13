namespace lazy_light_requests_gate.core.application.interfaces.buses
{
	/// <summary>
	/// Интерфейс для работы с шинами сообщений
	/// </summary>
	public interface IMessageBusService
	{
		Task PublishMessageAsync(string channelName, string routingKey, string message);
		Task StartListeningAsync(string channelName, CancellationToken cancellationToken);
		Task<bool> TestConnectionAsync();
		string GetBusType();
	}
}
