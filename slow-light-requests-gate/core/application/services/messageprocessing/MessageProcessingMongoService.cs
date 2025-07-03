using lazy_light_requests_gate.core.application.interfaces.repos;
using lazy_light_requests_gate.core.domain.entities;

namespace lazy_light_requests_gate.core.application.services.messageprocessing
{
	// Сервис обработки сообщений:
	public class MessageProcessingMongoService : MessageProcessingServiceBase
	{
		private readonly IMongoRepository<OutboxMessage> _outboxRepository;
		private readonly IMongoRepository<IncidentEntity> _incidentRepository;

		public MessageProcessingMongoService(
			IMongoRepository<OutboxMessage> outboxRepository,
			IMongoRepository<IncidentEntity> incidentRepository,
			ILogger<MessageProcessingMongoService> logger)
			: base(logger)
		{
			_outboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(outboxRepository));
			_incidentRepository = incidentRepository ?? throw new ArgumentNullException(nameof(incidentRepository));
		}

		protected override async Task SaveOutboxMessageAsync(OutboxMessage message)
		{
			await _outboxRepository.SaveMessageAsync(message);
		}

		protected override async Task SaveIncidentAsync(IncidentEntity incident)
		{
			await _incidentRepository.SaveMessageAsync(incident);
		}
	}
}

