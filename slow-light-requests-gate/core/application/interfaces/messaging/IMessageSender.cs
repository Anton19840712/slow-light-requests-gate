using lazy_light_requests_gate.core.application.interfaces.networking;

namespace lazy_light_requests_gate.core.application.interfaces.messaging
{
	/// <summary>
	/// Отсылает сообщение на внешний клиент.
	/// Используется на стороне сервера соответственно как правило для отправки отдельных инфраструктурных сообщений.
	/// </summary>
	public interface IMessageSender
	{
		Task SendMessagesToClientAsync(
			IConnectionContext connectionContext,
			string queueForListening,
			CancellationToken cancellationToken);
	}
}
