using lazy_light_requests_gate.common;
using lazy_light_requests_gate.repositories;
using lazy_light_requests_gate.services.lazy_light_requests_gate.services;

namespace lazy_light_requests_gate.services
{
	public class CleanupService<TRepository> : ICleanupService<TRepository> where TRepository : IBaseRepository<OutboxMessage>
	{
		private readonly ILogger<CleanupService<TRepository>> _logger;

		public CleanupService(ILogger<CleanupService<TRepository>> logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		public async Task StartCleanupAsync(TRepository repository, CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					int deletedCount = await repository.DeleteOldMessagesAsync(TimeSpan.FromSeconds(Constants.DefaultTtlDifferenceSeconds));
					if (deletedCount != 0)
					{
						_logger.LogInformation("Удалено {DeletedCount} старых сообщений из базы данных, коллекции outbox_messages.", deletedCount);
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
