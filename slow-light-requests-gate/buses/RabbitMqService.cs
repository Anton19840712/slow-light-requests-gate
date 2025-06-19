using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;
using lazy_light_requests_gate.temp;
using lazy_light_requests_gate.settings;

namespace lazy_light_requests_gate.buses;

/// <summary>
/// Сервис RabbitMQ с устойчивым соединением и поддержкой публикации и прослушивания сообщений.
/// </summary>
public class RabbitMqService : IRabbitMqService
{
	private readonly ILogger<RabbitMqService> _logger;
	private readonly IConnectionFactory _factory;
	private IConnection _persistentConnection;
	private IModel _listeningChannel;

	private Task _listenerTask;
	private CancellationTokenSource _listenerCts;

	private const int MaxRetryAttempts = 5;
	private const int RetryDelayMilliseconds = 3000;

	public RabbitMqService(ILogger<RabbitMqService> logger, IConnectionFactory factory)
	{
		_logger = logger;
		_factory = factory;
	}

	public string TransportName => "rabbitmq";

	public async Task<string> StartAsync(MessageBusBaseSettings config, CancellationToken cancellationToken)
	{
		try
		{
			if (config is not RabbitMqSettings rabbitMqConfig)
				throw new ArgumentException("StartAsync: Конфигурация должна быть типа RabbitMqSettings");

			// Установим соединение (лениво)
			_persistentConnection = PersistentConnection;

			_logger.LogInformation("RabbitMqService: Подключение установлено через фабрику {User}@/{VirtualHost}",_factory.UserName, _factory.VirtualHost);

			// Запуск слушателя, если очередь задана
			if (!string.IsNullOrWhiteSpace(rabbitMqConfig.ListenQueueName))
			{
				_listenerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				_listenerTask = Task.Run(() =>
					StartListeningAsync(rabbitMqConfig.ListenQueueName, _listenerCts.Token), _listenerCts.Token);
			}

			await Task.CompletedTask;
			return "RabbitMQ connection initialized successfully.";
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "RabbitMqService: Ошибка при инициализации подключения");
			throw;
		}
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		// Остановка слушателя
		if (_listenerCts != null)
		{
			_logger.LogInformation("RabbitMqService: Завершение слушателя...");
			_listenerCts.Cancel();

			try
			{
				await _listenerTask;
			}
			catch (OperationCanceledException)
			{
				_logger.LogInformation("RabbitMqService: Слушатель остановлен.");
			}
			_listenerCts.Dispose();
			_listenerCts = null;
		}

		// Закрытие канала
		_listeningChannel?.Close();
		_listeningChannel?.Dispose();
		_listeningChannel = null;

		// Закрытие подключения
		if (_persistentConnection?.IsOpen == true)
		{
			_persistentConnection.Close();
			_persistentConnection.Dispose();
			_logger.LogInformation("RabbitMqService: Подключение закрыто.");
		}
		else
		{
			_logger.LogWarning("RabbitMqService: Подключение уже было закрыто или не открыто.");
		}
		_persistentConnection = null;
	}

	private IConnection PersistentConnection
	{
		get
		{
			if (_persistentConnection != null && _persistentConnection.IsOpen)
				return _persistentConnection;

			int attempt = 0;

			while (attempt < MaxRetryAttempts)
			{
				try
				{
					_persistentConnection = _factory.CreateConnection();
					_logger.LogInformation("RabbitMqService: Подключение к RabbitMQ установлено.");
					return _persistentConnection;
				}
				catch (BrokerUnreachableException ex)
				{
					attempt++;
					_logger.LogWarning(ex, "Попытка {Attempt}/{MaxAttempts}: не удалось подключиться.", attempt, MaxRetryAttempts);

					if (attempt == MaxRetryAttempts)
					{
						_logger.LogError("RabbitMqService: Все попытки подключения исчерпаны.");
						throw;
					}

					Thread.Sleep(RetryDelayMilliseconds);
				}
			}

			throw new InvalidOperationException("RabbitMqService: Не удалось установить соединение.");
		}
	}

	private async Task StartListeningAsync(string queueName, CancellationToken stoppingToken, Func<string, Task>? onMessageReceived = null)
	{
		_listeningChannel = PersistentConnection.CreateModel();

		try
		{
			_listeningChannel.QueueDeclarePassive(queueName);
			_logger.LogInformation("Очередь {Queue} найдена. Подключаюсь.", queueName);
		}
		catch (OperationInterruptedException)
		{
			_logger.LogWarning("Очередь {Queue} не найдена. Создаю новую.", queueName);
			_listeningChannel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
		}

		var consumer = new EventingBasicConsumer(_listeningChannel);
		consumer.Received += async (_, ea) =>
		{
			var message = Encoding.UTF8.GetString(ea.Body.ToArray());

			if (onMessageReceived != null)
				await onMessageReceived(message);
			else
				_logger.LogInformation("RabbitMqService: Получено сообщение из {Queue}: {Message}", queueName, message);
		};

		_listeningChannel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);
		_logger.LogInformation("RabbitMqService: Слушаем очередь {Queue}...", queueName);

		try
		{
			await Task.Delay(Timeout.Infinite, stoppingToken);
		}
		catch (TaskCanceledException)
		{
			_logger.LogInformation("RabbitMqService: Слушатель остановлен для очереди {Queue}.", queueName);
		}
	}

	public async Task PublishMessageAsync(string queueName, string routingKey, string message)
	{
		if (_factory == null)
			throw new InvalidOperationException("RabbitMqService: Фабрика не задана.");

		await Task.Run(() =>
		{
			using var channel = PersistentConnection.CreateModel();

			channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

			var body = Encoding.UTF8.GetBytes(message);
			var properties = channel.CreateBasicProperties();
			properties.Persistent = true;
			properties.Headers = new Dictionary<string, object> { ["key"] = routingKey };

			channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: properties, body: body);

			_logger.LogInformation("RabbitMqService: Сообщение опубликовано в очередь {Queue}", queueName);
		});
	}

	public IConnection CreateConnection()
	{
		return PersistentConnection;
	}

	public async Task<string> WaitForResponseAsync(string queueName, int timeoutMilliseconds = 15000, CancellationToken cancellationToken = default)
	{
		using var channel = PersistentConnection.CreateModel();
		channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

		var completionSource = new TaskCompletionSource<string>();
		var consumer = new EventingBasicConsumer(channel);

		consumer.Received += (_, ea) =>
		{
			var response = Encoding.UTF8.GetString(ea.Body.ToArray());
			completionSource.TrySetResult(response);
		};

		channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);

		var completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(timeoutMilliseconds, cancellationToken));
		if (completedTask == completionSource.Task)
			return await completionSource.Task;

		_logger.LogWarning("RabbitMqService: Таймаут ожидания ответа из очереди {Queue}", queueName);
		return null;
	}
}
