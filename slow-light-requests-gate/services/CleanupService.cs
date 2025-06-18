using lazy_light_requests_gate.common;
using lazy_light_requests_gate.repositories;

namespace lazy_light_requests_gate.services
{
	public class CleanupService<TRepository> : ICleanupService<TRepository> where TRepository : IBaseRepository<OutboxMessage>
	{
		private readonly ILogger<CleanupService<TRepository>> _logger;
		private readonly IConfiguration _configuration;

		public CleanupService(ILogger<CleanupService<TRepository>> logger, IConfiguration configuration)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		}

		public async Task StartCleanupAsync(TRepository repository, CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					var cleanupIntervalSeconds = int.TryParse(_configuration["CleanupIntervalSeconds"], out var interval) ? interval : 1000;
					var deletedCount = await repository.DeleteOldMessagesAsync(TimeSpan.FromSeconds(cleanupIntervalSeconds));

					_logger.LogInformation("CleanupService: проверка старых сообщений завершена. Удалено: {DeletedCount} сообщений.", deletedCount);

					if (deletedCount > 0)
					{
						_logger.LogInformation("CleanupService: успешно удалено {DeletedCount} старых обработанных сообщений.", deletedCount);
					}
					else
					{
						_logger.LogDebug("CleanupService: старых обработанных сообщений для удаления не найдено.");
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Ошибка при очистке старых сообщений Outbox.");
				}

				await Task.Delay(TimeSpan.FromSeconds(Constants.DefaultCleanupIntervalSeconds), cancellationToken);
			}
		}
	}
}
