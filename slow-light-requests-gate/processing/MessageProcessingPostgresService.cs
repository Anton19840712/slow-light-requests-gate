using lazy_light_requests_gate.entities;
using lazy_light_requests_gate.models;
using lazy_light_requests_gate.repositories;

namespace lazy_light_requests_gate.processing
{
	// Сервис обработки сообщений:
	public class MessageProcessingPostgresService : IMessageProcessingService
	{
		private readonly IPostgresRepository<OutboxMessage> _outboxRepository;
		private readonly IPostgresRepository<IncidentEntity> _incidentRepository;
		private readonly ILogger<MessageProcessingPostgresService> _logger;

		public MessageProcessingPostgresService(
			IPostgresRepository<OutboxMessage> outboxRepository,
			IPostgresRepository<IncidentEntity> incidentRepository,
			ILogger<MessageProcessingPostgresService> logger)
		{
			_outboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(outboxRepository));
			_incidentRepository = incidentRepository ?? throw new ArgumentNullException(nameof(incidentRepository));
			_logger = logger;
		}

		public async Task ProcessIncomingMessageAsync(
			string message,
			string instanceModelQueueOutName,
			string instanceModelQueueInName,
			string host,
			int? port,
			string protocol)
		{
			try
			{
				var outboxMessage = new OutboxMessage
				{
					Id = Guid.NewGuid(),
					ModelType = ModelType.Outbox,
					EventType = EventTypes.Received,
					IsProcessed = false,
					ProcessedAt = DateTime.Now,
					InQueue = instanceModelQueueInName,
					OutQueue = instanceModelQueueOutName,
					Payload = message,
					RoutingKey = $"routing_key_{protocol}",
					CreatedAt = DateTime.UtcNow,
					Source = $"{protocol}-server-instance based on host: {host} and port {port}"
				};
				await _outboxRepository.SaveMessageAsync(outboxMessage);

				var incidentEntity = new IncidentEntity
				{
					Id = Guid.NewGuid(),
					Payload = message,
					CreatedAtUtc = DateTime.UtcNow,
					CreatedBy = $"{protocol}-server-instance",
					IpAddress = "default",
					UserAgent = $"{protocol}-server-instance",
					CorrelationId = Guid.NewGuid().ToString(),
					ModelType = "Incident",
					IsProcessed = false
				};
				await _incidentRepository.SaveMessageAsync(incidentEntity);
			}
			catch (Exception ex)
			{
				_logger.LogError("Ошибка при обработке сообщения.");
				_logger.LogError(ex.Message);
			}
		}
	}
}
