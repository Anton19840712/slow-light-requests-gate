using lazy_light_requests_gate.processing;
using lazy_light_requests_gate.repositories;
using lazy_light_requests_gate.services;

namespace lazy_light_requests_gate.background
{
	public class DynamicOutboxBackgroundService : BackgroundService
	{
		private readonly IServiceScopeFactory _serviceScopeFactory;
		private readonly IRabbitMqService _rabbitMqService;
		private readonly ILogger<DynamicOutboxBackgroundService> _logger;
		private readonly TimeSpan _delay = TimeSpan.FromSeconds(30);

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
			// Запускаем cleanup как параллельную задачу
			var cleanupTask = StartCleanupTask(stoppingToken);

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					using var scope = _serviceScopeFactory.CreateScope();
					var factory = scope.ServiceProvider.GetRequiredService<IMessageProcessingServiceFactory>();
					var currentDatabase = factory.GetCurrentDatabaseType();

					_logger.LogInformation("Processing outbox for database: {Database}", currentDatabase);

					if (currentDatabase == "postgres")
					{
						var postgresRepo = scope.ServiceProvider.GetRequiredService<IPostgresRepository<OutboxMessage>>();
						await ProcessOutboxMessages(postgresRepo, stoppingToken);
					}
					else
					{
						var mongoRepo = scope.ServiceProvider.GetRequiredService<IMongoRepository<OutboxMessage>>();
						await ProcessOutboxMessages(mongoRepo, stoppingToken);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error in DynamicOutboxBackgroundService");
				}

				await Task.Delay(_delay, stoppingToken);
			}

			// Ждем завершения cleanup задачи
			await cleanupTask;
		}

		private async Task StartCleanupTask(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					using var scope = _serviceScopeFactory.CreateScope();
					var factory = scope.ServiceProvider.GetRequiredService<IMessageProcessingServiceFactory>();
					var currentDatabase = factory.GetCurrentDatabaseType();

					if (currentDatabase == "postgres")
					{
						var postgresRepo = scope.ServiceProvider.GetRequiredService<IPostgresRepository<OutboxMessage>>();
						var cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService<IPostgresRepository<OutboxMessage>>>();
						await cleanupService.StartCleanupAsync(postgresRepo, stoppingToken);
					}
					else
					{
						var mongoRepo = scope.ServiceProvider.GetRequiredService<IMongoRepository<OutboxMessage>>();
						var cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService<IMongoRepository<OutboxMessage>>>();
						await cleanupService.StartCleanupAsync(mongoRepo, stoppingToken);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error in cleanup task");
					// Небольшая пауза перед повторной попыткой
					await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
				}
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

						_logger.LogInformation("Message {MessageId} published to queue {Queue}",
							message.Id, message.InQueue);
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
