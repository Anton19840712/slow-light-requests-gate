using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.core.application.services.common;
using lazy_light_requests_gate.core.domain.entities;

namespace lazy_light_requests_gate.core.application.services.messageprocessing
{
	public abstract class MessageProcessingServiceBase : IMessageProcessingService
	{
		protected readonly ILogger _logger;

		protected MessageProcessingServiceBase(ILogger logger)
		{
			_logger = logger;
		}

		public async Task ProcessForSaveIncomingMessageAsync(
			string message,
			string instanceModelQueueOutName,
			string instanceModelQueueInName,
			string host,
			int? port,
			string protocol)
		{
			try
			{
				var outboxMessage = MessageFactory.CreateOutboxMessage(message, instanceModelQueueInName, instanceModelQueueOutName, protocol, host, port);

				var incidentEntity = MessageFactory.CreateIncidentEntity(message, protocol);

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
