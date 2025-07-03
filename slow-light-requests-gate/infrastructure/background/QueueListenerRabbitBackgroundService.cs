using lazy_light_requests_gate.core.application.interfaces.listeners;
using lazy_light_requests_gate.core.application.services.listeners;

namespace lazy_light_requests_gate.infrastructure.background
{
	public class QueueListenerRabbitBackgroundService : BackgroundService
	{
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly ILogger<QueueListenerRabbitBackgroundService> _logger;
		private readonly IConfiguration _configuration;

		public QueueListenerRabbitBackgroundService(
			IServiceScopeFactory scopeFactory,
			ILogger<QueueListenerRabbitBackgroundService> logger,
			IConfiguration configuration)
		{
			_scopeFactory = scopeFactory;
			_logger = logger;
			_configuration = configuration;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var queueName = _configuration["QueueOut"];

			if (string.IsNullOrWhiteSpace(queueName))
			{
				_logger.LogWarning("QueueOut не задан в конфигурации. Слушатель не будет запущен.");
				return;
			}

			_logger.LogInformation("Запуск слушателя очереди: {QueueName}", queueName);

			try
			{
				using var scope = _scopeFactory.CreateScope();
				var queueListener = scope.ServiceProvider.GetRequiredService<IRabbitMqQueueListener<RabbitMqQueueListener>>();
				await queueListener.StartListeningAsync(queueName, stoppingToken);
				_logger.LogInformation("Слушатель очереди {QueueName} завершён.", queueName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Ошибка при запуске слушателя очереди {QueueName}", queueName);
			}
		}
	}
}
