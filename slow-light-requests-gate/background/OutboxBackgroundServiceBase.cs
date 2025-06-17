namespace lazy_light_requests_gate.background
{
	public abstract class OutboxBackgroundServiceBase<TRepository> : BackgroundService
			where TRepository : class
	{
		protected readonly TRepository _outboxRepository;
		protected readonly IRabbitMqService _rabbitMqService;
		protected readonly ILogger _logger;

		protected OutboxBackgroundServiceBase(
			TRepository outboxRepository,
			IRabbitMqService rabbitMqService,
			ILogger logger)
		{
			_outboxRepository = outboxRepository;
			_rabbitMqService = rabbitMqService;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken token)
		{
			_logger.LogInformation($"{GetType().Name}: фоновый процесс по сканированию и отправке сообщений из outbox table запущен.");

			_ = Task.Run(() => CleanupOldMessagesAsync(token), token);
			_logger.LogInformation($"{GetType().Name}: фоновый процесс по ликвидации отправленных сообщений из outbox table запущен.");

			while (!token.IsCancellationRequested)
			{
				try
				{
					var messages = await GetUnprocessedMessagesAsync();

					foreach (var message in messages)
					{
						_logger.LogInformation("");
						_logger.LogInformation($"Публикация сообщения: {message.Payload}");

						await _rabbitMqService.PublishMessageAsync(
							message.InQueue,
							message.RoutingKey,
							message.Payload);

						await MarkMessageAsProcessedAsync(message.Id);

						_logger.LogInformation("");
						_logger.LogInformation($"Обработано в Outbox: {message.Payload}");
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Ошибка при обработке Outbox.");
				}

				await Task.Delay(2000, token);
			}
		}

		private async Task CleanupOldMessagesAsync(CancellationToken token)
		{
			const int ttlDifference = 10;
			const int intervalInSeconds = 10;

			while (!token.IsCancellationRequested)
			{
				try
				{
					int deletedCount = await DeleteOldMessagesAsync(TimeSpan.FromSeconds(ttlDifference));
					if (deletedCount != 0)
					{
						_logger.LogInformation($"{GetType().Name}: удалено {deletedCount} старых сообщений из базы GatewayDB, коллекции outbox_messages.");
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Ошибка при очистке старых сообщений Outbox.");
				}

				await Task.Delay(TimeSpan.FromSeconds(intervalInSeconds), token);
			}
		}

		protected abstract Task<IEnumerable<OutboxMessage>> GetUnprocessedMessagesAsync();
		protected abstract Task MarkMessageAsProcessedAsync(Guid messageId);
		protected abstract Task<int> DeleteOldMessagesAsync(TimeSpan olderThan);
	}
}
