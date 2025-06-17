using lazy_light_requests_gate.repositories;
using lazy_light_requests_gate.services;

namespace lazy_light_requests_gate.background
{
	public class OutboxBackgroundServiceMongo : OutboxBackgroundServiceBase<IMongoRepository<OutboxMessage>>
	{
		public OutboxBackgroundServiceMongo(
			IServiceScopeFactory serviceScopeFactory,
			IRabbitMqService rabbitMqService,
			ILogger<OutboxBackgroundServiceMongo> logger,
			ICleanupService<IMongoRepository<OutboxMessage>> cleanupService)
			: base(serviceScopeFactory, rabbitMqService, logger, cleanupService)
		{
		}
	}
}
