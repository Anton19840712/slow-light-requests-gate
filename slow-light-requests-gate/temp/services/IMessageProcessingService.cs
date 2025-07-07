namespace application.interfaces.services
{
	// Интерфейс сервиса обработки сообщений:
	public interface IMessageProcessingService
	{
		Task ProcessIncomingMessageAsync(
			string message,
			string instanceModelQueueOutName,
			string instanceModelQueueInName,
			string host,
			int? port,
			string protocol);
	}
}
