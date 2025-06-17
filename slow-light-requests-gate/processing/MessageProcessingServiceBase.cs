using lazy_light_requests_gate.entities;
using lazy_light_requests_gate.models;

namespace lazy_light_requests_gate.processing
{
	public abstract class MessageProcessingServiceBase : IMessageProcessingService
	{
		protected readonly ILogger _logger;

		protected MessageProcessingServiceBase(ILogger logger)
		{
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

				await SaveOutboxMessageAsync(outboxMessage);
				await SaveIncidentAsync(incidentEntity);
			}
			catch (Exception ex)
			{
				_logger.LogError("Ошибка при обработке сообщения.");
				_logger.LogError(ex.Message);
			}
		}

		protected abstract Task SaveOutboxMessageAsync(OutboxMessage message);
		protected abstract Task SaveIncidentAsync(IncidentEntity incident);
	}
}
