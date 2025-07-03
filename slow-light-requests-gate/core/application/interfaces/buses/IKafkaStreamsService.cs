namespace lazy_light_requests_gate.core.application.interfaces.buses
{
	/// <summary>
	/// Интерфейс для работы с Kafka Streams
	/// </summary>
	public interface IKafkaStreamsService : IMessageBusService
	{
		Task StartAsync(CancellationToken cancellationToken = default);
		void Dispose();
	}
}
