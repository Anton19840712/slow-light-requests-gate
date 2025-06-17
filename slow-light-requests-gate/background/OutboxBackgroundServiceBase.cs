using lazy_light_requests_gate.repositories;

namespace lazy_light_requests_gate.background
{
	public class OutboxBackgroundServiceBase<TRepository> : BackgroundService
			where TRepository : IBaseRepository<OutboxMessage>
	{
		protected readonly TRepository _outboxRepository;
		protected readonly IRabbitMqService _rabbitMqService;
		protected readonly ILogger _logger;

		public OutboxBackgroundServiceBase(
			TRepository outboxRepository,
			IRabbitMqService rabbitMqService,
			ILogger<OutboxBackgroundServiceBase<TRepository>> logger)
		{
			_outboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(outboxRepository));
			_rabbitMqService = rabbitMqService ?? throw new ArgumentNullException(nameof(rabbitMqService));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

		protected virtual async Task<IEnumerable<OutboxMessage>> GetUnprocessedMessagesAsync()
		{
			return await _outboxRepository.GetUnprocessedMessagesAsync();
		}

		protected virtual async Task MarkMessageAsProcessedAsync(Guid messageId)
		{
			await _outboxRepository.MarkMessageAsProcessedAsync(messageId);
		}

		protected virtual async Task<int> DeleteOldMessagesAsync(TimeSpan olderThan)
		{
			return await _outboxRepository.DeleteOldMessagesAsync(olderThan);
		}
	}
}
