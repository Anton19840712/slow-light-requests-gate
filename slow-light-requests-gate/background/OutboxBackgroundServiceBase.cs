using lazy_light_requests_gate.repositories;
using lazy_light_requests_gate.services;

namespace lazy_light_requests_gate.background
{
	public class OutboxBackgroundServiceBase<TRepository> : BackgroundService
			where TRepository : IBaseRepository<OutboxMessage>
	{
		protected TRepository _outboxRepository;
		protected readonly IRabbitMqService _rabbitMqService;
		protected readonly ILogger _logger;
		protected readonly ICleanupService<TRepository> _cleanupService;
		protected readonly IServiceScopeFactory _serviceScopeFactory;
		public OutboxBackgroundServiceBase(
			IServiceScopeFactory serviceScopeFactory,
			IRabbitMqService rabbitMqService,
			ILogger<OutboxBackgroundServiceBase<TRepository>> logger,
			ICleanupService<TRepository> cleanupService)
		{
			_rabbitMqService = rabbitMqService ?? throw new ArgumentNullException(nameof(rabbitMqService));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
			_serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
		}

		protected override async Task ExecuteAsync(CancellationToken token)
		{
			_logger.LogInformation($"{GetType().Name}: фоновый процесс по сканированию и отправке сообщений из outbox table запущен.");

			using var scope = _serviceScopeFactory.CreateScope();
			var cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService<TRepository>>();
			_outboxRepository = scope.ServiceProvider.GetRequiredService<TRepository>();

			_ = Task.Run(() => cleanupService.StartCleanupAsync(_outboxRepository, token), token);
			_logger.LogInformation($"{GetType().Name}: фоновый процесс по ликвидации отправленных сообщений из outbox table запущен.");

			while (!token.IsCancellationRequested)
			{
				try
				{
					var unprocessedMessages = await GetUnprocessedMessagesAsync();
					if (unprocessedMessages.Any())
					{
						_logger.LogInformation($"{GetType().Name}: найдено {unprocessedMessages.Count()} неотправленных сообщений.");

						foreach (var message in unprocessedMessages)
						{
							try
							{
								// Публикуем сообщение в очередь_in
								await _rabbitMqService.PublishMessageAsync(
									message.InQueue,
									message.RoutingKey ?? message.InQueue,
									message.Payload);

								// Помечаем сообщение как обработанное
								message.IsProcessed = true;
								message.ProcessedAt = DateTime.UtcNow;
								await _outboxRepository.UpdateMessageAsync(message);

								_logger.LogInformation($"{GetType().Name}: сообщение {message.Id} успешно отправлено в очередь {message.InQueue}.");
							}
							catch (Exception ex)
							{
								_logger.LogError(ex, $"{GetType().Name}: ошибка при отправке сообщения {message.Id} в очередь {message.InQueue}.");
							}
						}
					}
					else
					{
						_logger.LogInformation($"{GetType().Name}: новых сообщений для отправки не найдено.");
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, $"{GetType().Name}: ошибка при сканировании outbox таблицы.");
				}

				await Task.Delay(TimeSpan.FromSeconds(5), token);
			}
		}

		private async Task<IEnumerable<OutboxMessage>> GetUnprocessedMessagesAsync()
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