using lazy_light_requests_gate.core.application.interfaces.buses;
using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.core.application.interfaces.repos;
using lazy_light_requests_gate.core.domain.entities;

namespace lazy_light_requests_gate.infrastructure.background;

public class DynamicOutboxCleanupService : BackgroundService
{
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly ILogger<DynamicOutboxCleanupService> _logger;
	private readonly TimeSpan _defaultCleanupInterval = TimeSpan.FromSeconds(30);

	public DynamicOutboxCleanupService(
		IServiceScopeFactory serviceScopeFactory,
		ILogger<DynamicOutboxCleanupService> logger)
	{
		_serviceScopeFactory = serviceScopeFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("DYNAMIC OUTBOX CLEANUP SERVICE STARTED");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessOutboxForCurrentConfiguration(stoppingToken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in outbox processing loop");
			}

			var delay = GetOptimalDelay();
			_logger.LogDebug("Next outbox processing in {Delay}", delay);
			await Task.Delay(delay, stoppingToken);
		}
	}

	private TimeSpan GetOptimalDelay()
	{
		try
		{
			using var scope = _serviceScopeFactory.CreateScope();
			var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

			var ttlSeconds = configuration.GetValue("OutboxMessageTtlSeconds", 10);

			// Чем меньше TTL, тем чаще нужно обрабатывать
			if (ttlSeconds <= 5)
				return TimeSpan.FromSeconds(2);
			else if (ttlSeconds <= 30)
				return TimeSpan.FromSeconds(5);
			else
				return TimeSpan.FromSeconds(10);
		}
		catch
		{
			return _defaultCleanupInterval;
		}
	}

	private async Task ProcessOutboxForCurrentConfiguration(CancellationToken stoppingToken)
	{
		try
		{
			using var scope = _serviceScopeFactory.CreateScope();

			// Получаем текущую конфигурацию базы данных и шины сообщений
			var databaseFactory = scope.ServiceProvider.GetRequiredService<IMessageProcessingServiceFactory>();
			var messageBusFactory = scope.ServiceProvider.GetRequiredService<IMessageBusServiceFactory>();

			var currentDatabase = databaseFactory.GetCurrentDatabaseType();
			var currentBus = messageBusFactory.GetCurrentBusType();

			_logger.LogDebug("Processing outbox for database: {Database}, message bus: {Bus}", currentDatabase, currentBus);

			if (currentDatabase == "postgres")
			{
				await ProcessPostgresOutbox(scope, currentBus, stoppingToken);
			}
			else if (currentDatabase == "mongo")
			{
				await ProcessMongoOutbox(scope, currentBus, stoppingToken);
			}
			else
			{
				_logger.LogWarning("Unknown database type: {Database}, skipping outbox processing", currentDatabase);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error determining current configuration for outbox processing");
		}
	}

	private async Task ProcessPostgresOutbox(IServiceScope scope, string currentBus, CancellationToken stoppingToken)
	{
		try
		{
			var postgresRepo = scope.ServiceProvider.GetRequiredService<IPostgresRepository<OutboxMessage>>();
			var messageBusFactory = scope.ServiceProvider.GetRequiredService<IMessageBusServiceFactory>();

			// Получаем необработанные сообщения
			var unprocessedMessages = await postgresRepo.GetUnprocessedMessagesAsync();

			if (!unprocessedMessages.Any())
			{
				_logger.LogDebug("No unprocessed outbox messages found in PostgreSQL");
				await CleanupOldMessages(postgresRepo);
				return;
			}

			_logger.LogInformation("Processing {Count} unprocessed outbox messages from PostgreSQL using {Bus}",
				unprocessedMessages.Count, currentBus.ToUpper());

			// Получаем текущий сервис шины сообщений postgres
			var busService = messageBusFactory.CreateMessageBusService(currentBus);

			var processedMessages = new List<OutboxMessage>();
			var failedMessages = new List<OutboxMessage>();

			foreach (var message in unprocessedMessages)
			{
				if (stoppingToken.IsCancellationRequested)
					break;

				try
				{
					// Публикуем сообщение через текущую шину postgres:
					await busService.PublishMessageAsync(
						message.InQueue,
						message.RoutingKey ?? message.InQueue,
						message.Payload);

					message.MarkAsProcessed();
					processedMessages.Add(message);

					_logger.LogDebug("Message {MessageId} published to {Bus} queue {Queue}",
						message.Id, currentBus, message.InQueue);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to publish message {MessageId} to {Bus}",
						message.Id, currentBus);

					message.MarkAsRetried(ex.Message);
					failedMessages.Add(message);
				}
			}

			// Обновляем статусы сообщений
			if (processedMessages.Any())
			{
				await postgresRepo.UpdateMessagesAsync(processedMessages);
				_logger.LogInformation("Successfully processed {Count} messages from PostgreSQL via {Bus}",
					processedMessages.Count, currentBus.ToUpper());
			}

			if (failedMessages.Any())
			{
				await postgresRepo.UpdateMessagesAsync(failedMessages);
				_logger.LogWarning("Failed to process {Count} messages from PostgreSQL via {Bus}",
					failedMessages.Count, currentBus.ToUpper());
			}

			await CleanupOldMessages(postgresRepo);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing PostgreSQL outbox messages");
		}
	}

	private async Task ProcessMongoOutbox(IServiceScope scope, string currentBus, CancellationToken stoppingToken)
	{
		try
		{
			var mongoRepo = scope.ServiceProvider.GetRequiredService<IMongoRepository<OutboxMessage>>();
			var messageBusFactory = scope.ServiceProvider.GetRequiredService<IMessageBusServiceFactory>();

			// Получаем необработанные сообщения
			var unprocessedMessages = await mongoRepo.GetUnprocessedMessagesAsync();

			if (!unprocessedMessages.Any())
			{
				_logger.LogDebug("No unprocessed outbox messages found in MongoDB");
				await CleanupOldMessages(mongoRepo);
				return;
			}

			_logger.LogInformation("Processing {Count} unprocessed outbox messages from MongoDB using {Bus}",
				unprocessedMessages.Count, currentBus.ToUpper());

			// Получаем текущий сервис шины сообщений
			var busService = messageBusFactory.CreateMessageBusService(currentBus);

			var processedMessages = new List<OutboxMessage>();
			var failedMessages = new List<OutboxMessage>();

			foreach (var message in unprocessedMessages)
			{
				if (stoppingToken.IsCancellationRequested)
					break;

				try
				{
					// Публикуем сообщение через текущую шину mongo
					await busService.PublishMessageAsync(
						message.InQueue,
						message.RoutingKey ?? message.InQueue,
						message.Payload);

					message.MarkAsProcessed();
					processedMessages.Add(message);

					_logger.LogDebug("Message {MessageId} published to {Bus} queue {Queue}",
						message.Id, currentBus, message.InQueue);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to publish message {MessageId} to {Bus}",
						message.Id, currentBus);

					message.MarkAsRetried(ex.Message);
					failedMessages.Add(message);
				}
			}

			// Обновляем статусы сообщений
			if (processedMessages.Any())
			{
				await mongoRepo.UpdateMessagesAsync(processedMessages);
				_logger.LogInformation("Successfully processed {Count} messages from MongoDB via {Bus}",
					processedMessages.Count, currentBus.ToUpper());
			}

			if (failedMessages.Any())
			{
				await mongoRepo.UpdateMessagesAsync(failedMessages);
				_logger.LogWarning("Failed to process {Count} messages from MongoDB via {Bus}",
					failedMessages.Count, currentBus.ToUpper());
			}

			await CleanupOldMessages(mongoRepo);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing MongoDB outbox messages");
		}
	}

	private async Task CleanupOldMessages<TRepo>(TRepo repository)
		where TRepo : IBaseRepository<OutboxMessage>
	{
		try
		{
			using var scope = _serviceScopeFactory.CreateScope();
			var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

			var ttlSeconds = configuration.GetValue("OutboxMessageTtlSeconds", 10);
			var cutoffDate = DateTime.UtcNow.AddSeconds(-ttlSeconds);
			// _logger.LogInformation("Starting MongoDB outbox cleanup. TTL: {TTL} seconds, Cutoff date: {CutoffDate}", ttlSeconds, cutoffDate);
			var deletedCount = await repository.DeleteOldRecordsAsync(cutoffDate, requireProcessed: true);

			if (deletedCount > 0)
			{
				_logger.LogDebug("Cleaned up {Count} old processed outbox messages", deletedCount);
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Error during outbox cleanup");
		}
	}
}