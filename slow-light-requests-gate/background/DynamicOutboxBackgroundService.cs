using lazy_light_requests_gate.messaging;
using lazy_light_requests_gate.repositories;
using lazy_light_requests_gate.services;

namespace lazy_light_requests_gate.background
{
	public class DynamicOutboxBackgroundService : BackgroundService
	{
		private readonly IServiceScopeFactory _serviceScopeFactory;
		private readonly IRabbitMqService _rabbitMqService;
		private readonly ILogger<DynamicOutboxBackgroundService> _logger;

		public DynamicOutboxBackgroundService(
			IServiceScopeFactory serviceScopeFactory,
			IRabbitMqService rabbitMqService,
			ILogger<DynamicOutboxBackgroundService> logger)
		{
			_serviceScopeFactory = serviceScopeFactory;
			_rabbitMqService = rabbitMqService;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// Запускаем задачу очистки в фоновом режиме
			_ = Task.Run(() => StartCleanupTaskAsync(stoppingToken), stoppingToken);

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					using var scope = _serviceScopeFactory.CreateScope();
					var factory = scope.ServiceProvider.GetRequiredService<IMessageProcessingServiceFactory>();
					var currentDatabase = factory.GetCurrentDatabaseType();

					_logger.LogInformation("DynamicOutboxBackgroundService: starting cleaning, getting-unprocessed-messages, publishing from outbox of database: {Database}", currentDatabase);

					if (currentDatabase == "postgres")
					{
						var repo = scope.ServiceProvider.GetRequiredService<IPostgresRepository<OutboxMessage>>();
						await ProcessOutboxMessages(repo, stoppingToken);
					}
					else
					{
						var repo = scope.ServiceProvider.GetRequiredService<IMongoRepository<OutboxMessage>>();
						await ProcessOutboxMessages(repo, stoppingToken);
					}

					// Интервал между итерациями обработки
					await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Ошибка в DynamicOutboxBackgroundService");
				}
			}
		}

		private async Task StartCleanupTaskAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				using var scope = _serviceScopeFactory.CreateScope();

				try
				{
					var cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService>();

					await cleanupService.StartCleanupAsync(scope.ServiceProvider, stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Ошибка в задаче очистки Outbox");
				}

				// Пауза на случай быстрого выхода из StartCleanupAsync
				await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
			}
		}

		private async Task ProcessOutboxMessages<TRepo>(TRepo repository, CancellationToken cancellationToken)
			where TRepo : IBaseRepository<OutboxMessage>
		{
			try
			{
				var messages = await repository.GetUnprocessedMessagesAsync();

				foreach (var message in messages)
				{
					if (cancellationToken.IsCancellationRequested)
						break;

					try
					{
						await _rabbitMqService.PublishMessageAsync(
							message.InQueue,
							message.RoutingKey ?? message.InQueue,
							message.Payload);

						message.IsProcessed = true;
						message.ProcessedAt = DateTime.UtcNow;

						await repository.UpdateMessageAsync(message);

						_logger.LogInformation("Message {MessageId} published to queue {Queue}", message.Id, message.InQueue);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Failed to publish message {MessageId}", message.Id);
						await repository.UpdateMessageAsync(message);
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing outbox messages");
			}
		}
	}
}
