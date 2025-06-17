using lazy_light_requests_gate.repositories;
using lazy_light_requests_gate;
using lazy_light_requests_gate.background;

public class OutboxPostgresBackgroundService : OutboxBackgroundServiceBase<IPostgresRepository<OutboxMessage>>
{
	public OutboxPostgresBackgroundService(
		IPostgresRepository<OutboxMessage> outboxRepository,
		IRabbitMqService rabbitMqService,
		ILogger<OutboxPostgresBackgroundService> logger)
		: base(outboxRepository, rabbitMqService, logger)
	{
	}

	protected override async Task<IEnumerable<OutboxMessage>> GetUnprocessedMessagesAsync()
	{
		return await _outboxRepository.GetUnprocessedMessagesAsync();
	}

	protected override async Task MarkMessageAsProcessedAsync(Guid messageId)
	{
		await _outboxRepository.MarkMessageAsProcessedAsync(messageId);
	}

	protected override async Task<int> DeleteOldMessagesAsync(TimeSpan olderThan)
	{
		return await _outboxRepository.DeleteOldMessagesAsync(olderThan);
	}
}
