using lazy_light_requests_gate.entities;
using lazy_light_requests_gate.listenersrabbit;
using lazy_light_requests_gate.repositories;
using listenersrabbit;

/// <summary>
/// Сервис слушает очередь bpm
/// </summary>
public class QueueListenerRabbitPostgresBackgroundService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<QueueListenerRabbitPostgresBackgroundService> _logger;

	public QueueListenerRabbitPostgresBackgroundService(IServiceScopeFactory scopeFactory, ILogger<QueueListenerRabbitPostgresBackgroundService> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("QueueListenerRabbitPostgresBackgroundService: пробуем запуск фонового сервиса прослушивания очередей.");

		try
		{
			using var scope = _scopeFactory.CreateScope();
			var queueListener = scope.ServiceProvider.GetRequiredService<IRabbitMqQueueListener<RabbitMqQueueListener>>();
			var queuesRepository = scope.ServiceProvider.GetRequiredService<IPostgresRepository<QueuesEntity>>();

			var elements = await queuesRepository.GetAllAsync();

			if (elements == null || !elements.Any())
			{
				_logger.LogInformation("Нет конкретных очередей для прослушивания. Слушатели rabbit не будут запущены.");
				return;
			}

			var validQueues = elements
				.Where(e => !string.IsNullOrWhiteSpace(e.OutQueueName))
				.ToList();

			if (!validQueues.Any())
			{
				_logger.LogWarning("Все записи в очередях имеют пустой OutQueueName. Слушатели не будут запущены.");
				return;
			}

			var listeningTasks = validQueues
				.Select(element =>
				{
					_logger.LogInformation("Инициализация слушателя очереди: {Queue}", element.OutQueueName);
					return Task.Run(() => queueListener.StartListeningAsync(element.OutQueueName, stoppingToken), stoppingToken);
				})
				.ToList();

			await Task.WhenAll(listeningTasks);

			foreach (var item in validQueues)
			{
				_logger.LogInformation("Слушатель для очереди {Queue} запущен.", item.OutQueueName);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Ошибка при запуске слушателей очередей.");
		}
	}
}
