using lazy_light_requests_gate.entities;
using lazy_light_requests_gate.repositories;

namespace lazy_light_requests_gate.background
{
	public class QueueListenerBackgroundServiceMongo : QueueListenerBackgroundServiceBase<IMongoRepository<QueuesEntity>>
	{
		public QueueListenerBackgroundServiceMongo(
			IServiceScopeFactory scopeFactory,
			ILogger<QueueListenerBackgroundServiceMongo> logger)
			: base(scopeFactory, logger)
		{
		}
	}
}
