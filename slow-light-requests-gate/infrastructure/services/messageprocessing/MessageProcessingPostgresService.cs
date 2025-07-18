﻿using lazy_light_requests_gate.core.application.interfaces.repos;
using lazy_light_requests_gate.core.domain.entities;

namespace lazy_light_requests_gate.infrastructure.services.messageprocessing
{
	// Сервис обработки сообщений:
	public class MessageProcessingPostgresService : MessageProcessingServiceBase
	{
		private readonly IPostgresRepository<OutboxMessage> _outboxRepository;
		private readonly IPostgresRepository<IncidentEntity> _incidentRepository;

		public MessageProcessingPostgresService(
			IPostgresRepository<OutboxMessage> outboxRepository,
			IPostgresRepository<IncidentEntity> incidentRepository,
			ILogger<MessageProcessingPostgresService> logger)
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
