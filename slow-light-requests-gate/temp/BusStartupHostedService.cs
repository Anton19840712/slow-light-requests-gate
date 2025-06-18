namespace lazy_light_requests_gate.temp
{
	public class BusStartupHostedService : BackgroundService
	{
		private readonly IUnifiedMessageBusManager _busManager;
		private readonly ILogger<BusStartupHostedService> _logger;

		public BusStartupHostedService(IUnifiedMessageBusManager busManager, ILogger<BusStartupHostedService> logger)
		{
			_busManager = busManager;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			try
			{
				_logger.LogInformation("Запуск шины из фоновой службы...");
				await _busManager.StartFromConfigFileAsync(stoppingToken);
				_logger.LogInformation("Шина согласно изначальным конфигурационным данным запущена.");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Ошибка при запуске шины.");
			}
		}
	}
}
