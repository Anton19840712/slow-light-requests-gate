namespace lazy_light_requests_gate.core.application.interfaces.listeners
{
	public interface IRabbitMqQueueListener
	{
		Task StartListeningAsync(
			string queueOutName,
			CancellationToken stoppingToken,
			string pathToPushIn = null,
			Func<string, Task> onMessageReceived = null);
		void StopListening();
	}
}
