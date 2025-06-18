using lazy_light_requests_gate.common;
using lazy_light_requests_gate.repositories;

namespace lazy_light_requests_gate.services
{
	public class CleanupService<TRepository> : ICleanupService<TRepository> where TRepository : IBaseRepository<OutboxMessage>
	{
		private readonly ILogger<CleanupService<TRepository>> _logger;
		private readonly IConfiguration _configuration;
		private readonly int _delay;
		public CleanupService(ILogger<CleanupService<TRepository>> logger, IConfiguration configuration)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

			if (!int.TryParse(_configuration["CleanupIntervalSeconds"], out _delay))
			{
				_delay = 10; // значение по умолчанию
				_logger.LogWarning("Не удалось прочитать CleanupIntervalSeconds. Установлено значение по умолчанию: 10 секунд");
			}
			else
			{
				_logger.LogInformation("Инициализирован с интервалом очистки: {Delay} секунд", _delay);
			}
		}

		public async Task StartCleanupAsync(TRepository repository, CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					// Время жизни сообщений (после которого они удаляются)
					var messageTtlSeconds = int.TryParse(_configuration["MessageTtlSeconds"], out var ttl) ? ttl : 10;
					var deletedCount = await repository.DeleteOldMessagesAsync(TimeSpan.FromSeconds(messageTtlSeconds));

					_logger.LogInformation("CleanupService: проверка старых сообщений завершена. Удалено: {DeletedCount} сообщений (TTL: {TtlSeconds} сек).",
						deletedCount, messageTtlSeconds);

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

				// Интервал между запусками очистки:
				await Task.Delay(TimeSpan.FromSeconds(_delay), cancellationToken);
			}
		}
	}
}
