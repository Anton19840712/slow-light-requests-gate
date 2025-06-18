using lazy_light_requests_gate.services.lazy_light_requests_gate.services;

namespace lazy_light_requests_gate.background
{
	public class IncidentCleanupBackgroundService : BackgroundService
	{
		private readonly IServiceScopeFactory _serviceScopeFactory;
		private readonly ILogger<IncidentCleanupBackgroundService> _logger;

		public IncidentCleanupBackgroundService(
			IServiceScopeFactory serviceScopeFactory,
			ILogger<IncidentCleanupBackgroundService> logger)
		{
			_serviceScopeFactory = serviceScopeFactory;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("IncidentCleanupBackgroundService запущен");

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					using var scope = _serviceScopeFactory.CreateScope();
					var incidentCleanupService = scope.ServiceProvider.GetRequiredService<IIncidentCleanupService>();

					await incidentCleanupService.StartIncidentCleanupAsync(scope.ServiceProvider, stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Ошибка в IncidentCleanupBackgroundService");
				}

				// Пауза на случай быстрого выхода из StartIncidentCleanupAsync
				await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
			}
		}
	}
}
