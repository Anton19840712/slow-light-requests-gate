using lazy_light_requests_gate.core.application.helpers;
using lazy_light_requests_gate.core.domain.entities;

namespace lazy_light_requests_gate.infrastructure.services.messageprocessing
{
	public abstract class MessageProcessingServiceBase //: IMessageProcessingService
	{
		protected readonly ILogger _logger;

		protected MessageProcessingServiceBase(ILogger logger)
		{
			_logger = logger;
		}

		public async Task ProcessForSaveIncomingMessageAsync(
			string message,
			string channelOut,
			string channelIn,
			string host,
			int? port,
			string protocol)
		{
			try
			{
				var outboxMessage = MessageFactory.CreateOutboxMessage(message, channelIn, channelOut, protocol, host, port);

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
