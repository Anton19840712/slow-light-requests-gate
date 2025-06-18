using lazy_light_requests_gate.repositories;
using lazy_light_requests_gate.messaging;

namespace lazy_light_requests_gate.services
{
	public class CleanupService : ICleanupService
	{
		private readonly ILogger<CleanupService> _logger;
		private readonly IConfiguration _configuration;
		private readonly int _delay;

		public CleanupService(ILogger<CleanupService> logger, IConfiguration configuration)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

			if (!int.TryParse(_configuration["CleanupIntervalSeconds"], out _delay))
			{
				_delay = 10;
				_logger.LogWarning("Не удалось прочитать CleanupIntervalSeconds. Используется значение по умолчанию: {DefaultDelay} сек.", _delay);
			}
			else
			{
				_logger.LogInformation("CleanupService инициализирован с интервалом: {Delay} сек.", _delay);
			}
		}

		public async Task StartCleanupAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					using var scope = serviceProvider.CreateScope();

					var factory = scope.ServiceProvider.GetRequiredService<IMessageProcessingServiceFactory>();
					var currentDatabase = factory.GetCurrentDatabaseType();

					int ttlSeconds = int.TryParse(_configuration["OutboxMessageTtlSeconds"], out var ttl) ? ttl : 10;
					var olderThan = TimeSpan.FromSeconds(ttlSeconds);
					int deletedCount = 0;

					if (currentDatabase == "postgres")
					{
						var postgresRepo = scope.ServiceProvider.GetRequiredService<IPostgresRepository<OutboxMessage>>();
						deletedCount = await postgresRepo.DeleteOldMessagesAsync(olderThan);
					}
					else if (currentDatabase == "mongo")
					{
						var mongoRepo = scope.ServiceProvider.GetRequiredService<IMongoRepository<OutboxMessage>>();
						deletedCount = await mongoRepo.DeleteOldMessagesAsync(olderThan);
					}
					else
					{
						_logger.LogWarning("Неизвестный тип базы данных: {Database}", currentDatabase);
					}

					if (deletedCount > 0)
					{
						_logger.LogInformation("Удалено {DeletedCount} сообщений старше {TtlSeconds} сек.", deletedCount, ttlSeconds);
					}
					else
					{
						_logger.LogDebug("Сообщений для удаления не найдено. TTL: {TtlSeconds} сек.", ttlSeconds);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Ошибка при очистке Outbox");
				}

				await Task.Delay(TimeSpan.FromSeconds(_delay), cancellationToken);
			}
		}
	}
}
