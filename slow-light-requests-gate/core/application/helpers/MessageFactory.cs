using lazy_light_requests_gate.core.domain.constants;
using lazy_light_requests_gate.core.domain.entities;
using lazy_light_requests_gate.presentation.enums;

namespace lazy_light_requests_gate.core.application.helpers
{
	public static class MessageFactory
	{
		public static OutboxMessage CreateOutboxMessage(
			string message,
			string instanceModelQueueInName,
			string instanceModelQueueOutName,
			string protocol,
			string host,
			int? port)
		{
			return new OutboxMessage
			{
				Id = Guid.NewGuid(),
				ModelType = ModelType.Outbox,
				EventType = EventTypes.Received,
				IsProcessed = false,
				ProcessedAt = DateTime.Now,
				InQueue = instanceModelQueueInName,
				OutQueue = instanceModelQueueOutName,
				Payload = message,
				RoutingKey = $"{Constants.RoutingKeyPrefix}{protocol}",
				CreatedAt = DateTime.UtcNow,
				Source = $"{protocol}-server-instance based on host: {host} and port {port}"
			};
		}

		public static IncidentEntity CreateIncidentEntity(
			string message,
			string protocol)
		{
			return new IncidentEntity
			{
				Id = Guid.NewGuid(),
				Payload = message,
				CreatedAt = DateTime.UtcNow,
				CreatedBy = $"{protocol}-server-instance",
				IpAddress = Constants.DefaultIpAddress,
				UserAgent = $"{protocol}-{Constants.DefaultUserAgent}",
				CorrelationId = Guid.NewGuid().ToString(),
				ModelType = "Incident",
				IsProcessed = false
			};
		}
	}
}
