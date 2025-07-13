using application.interfaces.networking;

namespace application.interfaces.services
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
