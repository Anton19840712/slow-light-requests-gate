using lazy_light_requests_gate.entities;
using lazy_light_requests_gate.listenersrabbit;
using lazy_light_requests_gate.repositories;
using listenersrabbit;

public class QueueListenerBackgroundService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<QueueListenerBackgroundService> _logger;

	public QueueListenerBackgroundService(IServiceScopeFactory scopeFactory, ILogger<QueueListenerBackgroundService> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("QueueListenerBackgroundService: запуск фонового сервиса прослушивания очередей.");

		try
		{
			using var scope = _scopeFactory.CreateScope();
			var queueListener = scope.ServiceProvider.GetRequiredService<IRabbitMqQueueListener<RabbitMqQueueListener>>();
			var queuesRepository = scope.ServiceProvider.GetRequiredService<IMongoRepository<QueuesEntity>>();

			var elements = await queuesRepository.GetAllAsync();

			if (elements == null || !elements.Any())
			{
				_logger.LogInformation("Нет конкретных очередей для прослушивания. Слушатели rabbit не будут запущены.");
				return;
			}

			var listeningTasks = elements
				.Select(element => Task.Run(() => queueListener.StartListeningAsync(element.OutQueueName, stoppingToken), stoppingToken))
				.ToList();

			await Task.WhenAll(listeningTasks);

			foreach (var item in elements)
			{
				_logger.LogInformation($"Слушатель для очереди {item.OutQueueName} запущен.");
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Ошибка при запуске слушателей очередей.");
		}
	}
}
