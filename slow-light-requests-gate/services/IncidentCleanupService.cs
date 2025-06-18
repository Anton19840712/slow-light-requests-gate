using lazy_light_requests_gate.entities;
using lazy_light_requests_gate.messaging;
using lazy_light_requests_gate.repositories;

namespace lazy_light_requests_gate.services
{
	namespace lazy_light_requests_gate.services
	{
		public class IncidentCleanupService : IIncidentCleanupService
		{
			private readonly ILogger<IncidentCleanupService> _logger;
			private readonly IConfiguration _configuration;
			private readonly int _ttlMonths;

			public IncidentCleanupService(ILogger<IncidentCleanupService> logger, IConfiguration configuration)
			{
				_logger = logger ?? throw new ArgumentNullException(nameof(logger));
				_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

				if (!int.TryParse(_configuration["IncidentEntitiesTtlMonths"], out _ttlMonths))
				{
					_ttlMonths = 60; // 5 лет по умолчанию
					_logger.LogWarning("Не удалось прочитать IncidentEntitiesTtlMonths. Используется значение по умолчанию: {DefaultTtl} месяцев.", _ttlMonths);
				}
				else
				{
					_logger.LogInformation("IncidentCleanupService инициализирован с TTL: {TtlMonths} месяцев.", _ttlMonths);
				}
			}

			public async Task StartIncidentCleanupAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
			{
				// Вычисляем интервал между очистками (например, раз в месяц)
				var cleanupInterval = TimeSpan.FromDays(30); // Проверяем раз в месяц

				while (!cancellationToken.IsCancellationRequested)
				{
					try
					{
						using var scope = serviceProvider.CreateScope();

						var factory = scope.ServiceProvider.GetRequiredService<IMessageProcessingServiceFactory>();
						var currentDatabase = factory.GetCurrentDatabaseType();

						var olderThan = TimeSpan.FromDays(_ttlMonths * 30); // Примерно 30 дней в месяце
						int deletedCount = 0;

						if (currentDatabase == "postgres")
						{
							var postgresRepo = scope.ServiceProvider.GetRequiredService<IPostgresRepository<IncidentEntity>>();
							deletedCount = await postgresRepo.DeleteOldMessagesAsync(olderThan);
						}
						else if (currentDatabase == "mongo")
						{
							var mongoRepo = scope.ServiceProvider.GetRequiredService<IMongoRepository<IncidentEntity>>();
							deletedCount = await mongoRepo.DeleteOldMessagesAsync(olderThan);
						}
						else
						{
							_logger.LogWarning("Неизвестный тип базы данных: {Database}", currentDatabase);
						}

						if (deletedCount > 0)
						{
							_logger.LogInformation("Удалено {DeletedCount} записей IncidentEntity старше {TtlMonths} месяцев.", deletedCount, _ttlMonths);
						}
						else
						{
							_logger.LogDebug("Записей IncidentEntity для удаления не найдено. TTL: {TtlMonths} месяцев.", _ttlMonths);
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Ошибка при очистке IncidentEntity");
					}

					await Task.Delay(cleanupInterval, cancellationToken);
				}
			}
		}
	}
}
