namespace lazy_light_requests_gate.core.application.interfaces.messageprocessing
{
	// Интерфейс сервиса обработки сообщений:
	public interface IMessageProcessingService
	{
		Task ProcessForSaveIncomingMessageAsync(
			string message,
			string instanceModelQueueOutName,
			string instanceModelQueueInName,
			string host,
			int? port,
			string protocol);
	}
}
