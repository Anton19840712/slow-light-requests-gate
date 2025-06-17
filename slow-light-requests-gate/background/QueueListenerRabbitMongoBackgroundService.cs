using lazy_light_requests_gate.entities;
using lazy_light_requests_gate.repositories;
using lazy_light_requests_gate.background;

/// <summary>
/// Сервис слушает очередь bpm для MongoDB
/// </summary>
public class QueueListenerRabbitMongoBackgroundService : QueueListenerBackgroundServiceBase<IMongoRepository<QueuesEntity>>
{
	public QueueListenerRabbitMongoBackgroundService(
		IServiceScopeFactory scopeFactory,
		ILogger<QueueListenerRabbitMongoBackgroundService> logger)
		: base(scopeFactory, logger)
	{
	}
}