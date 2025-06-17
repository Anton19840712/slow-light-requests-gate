using lazy_light_requests_gate.repositories;
using lazy_light_requests_gate.services;

namespace lazy_light_requests_gate.background
{
	public class OutboxBackgroundServicePostgres : OutboxBackgroundServiceBase<IPostgresRepository<OutboxMessage>>
	{
		public OutboxBackgroundServicePostgres(
			IServiceScopeFactory serviceScopeFactory,
			IRabbitMqService rabbitMqService,
			ILogger<OutboxBackgroundServicePostgres> logger,
			ICleanupService<IPostgresRepository<OutboxMessage>> cleanupService)
			: base(serviceScopeFactory, rabbitMqService, logger, cleanupService)
		{
		}
	}
}
