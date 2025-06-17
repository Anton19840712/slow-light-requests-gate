using lazy_light_requests_gate.repositories;
using lazy_light_requests_gate.services.lazy_light_requests_gate.services;

namespace lazy_light_requests_gate.background
{
	public class OutboxBackgroundServiceBase<TRepository> : BackgroundService
			where TRepository : IBaseRepository<OutboxMessage>
	{
		protected readonly TRepository _outboxRepository;
		protected readonly IRabbitMqService _rabbitMqService;
		protected readonly ILogger _logger;
		protected readonly ICleanupService<TRepository> _cleanupService;

		public OutboxBackgroundServiceBase(
			TRepository outboxRepository,
			IRabbitMqService rabbitMqService,
			ILogger<OutboxBackgroundServiceBase<TRepository>> logger,
			ICleanupService<TRepository> cleanupService)
		{
			_outboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(outboxRepository));
			_rabbitMqService = rabbitMqService ?? throw new ArgumentNullException(nameof(rabbitMqService));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
		}

		protected override async Task ExecuteAsync(CancellationToken token)
		{
			_logger.LogInformation($"{GetType().Name}: фоновый процесс по сканированию и отправке сообщений из outbox table запущен.");

			_ = Task.Run(() => _cleanupService.StartCleanupAsync(_outboxRepository, token), token);
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
