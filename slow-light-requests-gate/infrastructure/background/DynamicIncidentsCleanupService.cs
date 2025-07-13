using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.core.application.interfaces.repos;
using lazy_light_requests_gate.core.domain.entities;

namespace lazy_light_requests_gate.infrastructure.background
{
	public class DynamicIncidentCleanupService : BackgroundService
	{
		private readonly IServiceScopeFactory _serviceScopeFactory;
		private readonly ILogger<DynamicIncidentCleanupService> _logger;
		private readonly TimeSpan _defaultCleanupInterval = TimeSpan.FromHours(1); // Дефолтный интервал

		public DynamicIncidentCleanupService(
			IServiceScopeFactory serviceScopeFactory,
			ILogger<DynamicIncidentCleanupService> logger)
		{
			_serviceScopeFactory = serviceScopeFactory;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("DYNAMIC INCIDENT CLEANUP SERVICE STARTED");

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await CleanupIncidentsForCurrentDatabase(stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error in incident cleanup loop");
				}

				var delay = GetCleanupInterval();
				_logger.LogDebug("Next incident cleanup in {Delay}", delay);
				await Task.Delay(delay, stoppingToken);
			}
		}

		private TimeSpan GetCleanupInterval()
		{
			try
			{
				using var scope = _serviceScopeFactory.CreateScope();
				var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

				// Читаем из конфигурации IncidentEntitiesTtlMonths для расчета интервала
				var ttlMonths = configuration.GetValue("IncidentEntitiesTtlMonths", 60);

				if (ttlMonths == 0)
				{
					return TimeSpan.FromSeconds(2); // Каждые 2 секунды при TTL == 0
				}

				// Для больших TTL можем реже проверять
				if (ttlMonths >= 12)
					return TimeSpan.FromHours(24); // Раз в день
				else if (ttlMonths >= 6)
					return TimeSpan.FromHours(12); // Два раза в день
				else
					return TimeSpan.FromHours(6);  // Четыре раза в день
			}
			catch
			{
				return _defaultCleanupInterval; // fallback
			}
		}

		private async Task CleanupIncidentsForCurrentDatabase(CancellationToken stoppingToken)
		{
			try
			{
				using var scope = _serviceScopeFactory.CreateScope();
				var factory = scope.ServiceProvider.GetRequiredService<IMessageProcessingServiceFactory>();
				var currentDatabase = factory.GetCurrentDatabaseType();

				_logger.LogDebug("Cleaning up incidents for database: {Database}", currentDatabase);

				if (currentDatabase == "postgres")
				{
					await CleanupPostgresIncidents(scope, stoppingToken);
				}
				else if (currentDatabase == "mongo")
				{
					await CleanupMongoIncidents(scope, stoppingToken);
				}
				else
				{
					_logger.LogWarning("Unknown database type: {Database}, skipping incident cleanup", currentDatabase);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error determining current database type for incident cleanup");
			}
		}

		private async Task CleanupPostgresIncidents(IServiceScope scope, CancellationToken stoppingToken)
		{
			try
			{
				var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
				var ttlMonths = configuration.GetValue("IncidentEntitiesTtlMonths", 60);

				// Вычисляем дату отсечения
				var cutoffDate = DateTime.UtcNow.AddMonths(-ttlMonths);

				_logger.LogInformation("Starting PostgreSQL incident cleanup. TTL: {TTL} months, Cutoff date: {CutoffDate}",
					ttlMonths, cutoffDate);

				var postgresRepo = scope.ServiceProvider.GetRequiredService<IPostgresRepository<IncidentEntity>>();

				// Передаем cutoffDate напрямую
				var deletedCount = await postgresRepo.DeleteOldRecordsAsync(cutoffDate);

				if (deletedCount > 0)
				{
					_logger.LogInformation("Successfully deleted {Count} old incidents from PostgreSQL (older than {CutoffDate})",
						deletedCount, cutoffDate);
				}
				else
				{
					_logger.LogDebug("No old incidents found for cleanup in PostgreSQL");
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error cleaning up PostgreSQL incidents");
			}
		}

		private async Task CleanupMongoIncidents(IServiceScope scope, CancellationToken stoppingToken)
		{
			try
			{
				var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
				var ttlMonths = configuration.GetValue("IncidentEntitiesTtlMonths", 60);

				// удаляем incidents сообщения, которые были созданы столько-то месяцев назад (если для теста укажем в конфиге
				// (IncidentEntitiesTtlMonths 0) месяцев назад, то все удалится раньше, чем сейчас)
				// используйте ttlMonths!
				var cutoffDate = DateTime.UtcNow.AddMonths(-ttlMonths);

				// _logger.LogInformation("Starting MongoDB incident cleanup. TTL: {TTL} months, Cutoff date: {CutoffDate}", ttlMonths, cutoffDate);

				var mongoRepo = scope.ServiceProvider.GetRequiredService<IMongoRepository<IncidentEntity>>();
				var deletedCount = await mongoRepo.DeleteOldRecordsAsync(cutoffDate);

				if (deletedCount > 0)
				{
					_logger.LogInformation("Successfully deleted {Count} old incidents from MongoDB (older than {CutoffDate})",
						deletedCount, cutoffDate);
				}
				else
				{
					_logger.LogDebug("No old incidents found for cleanup in MongoDB");
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error cleaning up MongoDB incidents");
			}
		}
	}
}