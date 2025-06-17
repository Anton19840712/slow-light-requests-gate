using lazy_light_requests_gate.repositories;
using lazy_light_requests_gate.services;

namespace lazy_light_requests_gate.background
{
	public class OutboxBackgroundServicePostgres : OutboxBackgroundServiceBase<IPostgresRepository<OutboxMessage>>
	{
		public OutboxBackgroundServicePostgres(
			IPostgresRepository<OutboxMessage> outboxRepository,
			IRabbitMqService rabbitMqService,
			ILogger<OutboxBackgroundServicePostgres> logger,
			ICleanupService<IPostgresRepository<OutboxMessage>> cleanupService)
			: base(outboxRepository, rabbitMqService, logger, cleanupService)
		{
		}
	}
}
