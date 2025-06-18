using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;
using lazy_light_requests_gate.temp;
using lazy_light_requests_gate.settings;

namespace lazy_light_requests_gate.buses
{
	/// <summary>
	/// Сервис RabbitMQ с устойчивым соединением и поддержкой публикации сообщений.
	/// </summary>
	public class RabbitMqService : IRabbitMqService
	{
		private readonly ILogger<RabbitMqService> _logger;
		private IConnection _persistentConnection;
		private ConnectionFactory _factory;

		private const int MaxRetryAttempts = 5;
		private const int RetryDelayMilliseconds = 3000;

		public RabbitMqService(ILogger<RabbitMqService> logger)
		{
			_logger = logger;
		}
		public string TransportName => "rabbitmq";

		public async Task<string> StartAsync(MessageBusBaseSettings config, CancellationToken cancellationToken)
		{
			try
			{
				if (config is not RabbitMqSettings rabbitMqConfig)
					throw new ArgumentException("StartAsync: Конфигурация должна быть типа RabbitMqSettings");

				_factory = new ConnectionFactory
				{
					Uri = rabbitMqConfig.GetAmqpUri(),
					UserName = rabbitMqConfig.UserName,
					Password = rabbitMqConfig.Password,
					VirtualHost = rabbitMqConfig.VirtualHost,
					RequestedHeartbeat = TimeSpan.FromSeconds(ushort.TryParse(rabbitMqConfig.Heartbeat?.ToString(), out var hb) ? hb : 60)
				};

				_ = PersistentConnection;

				_logger.LogInformation("RabbitMqService: Установлена конфигурация подключения {User}@{Host}:{Port}-{VirtualHost}",
					_factory.UserName, _factory.HostName, _factory.Port, _factory.VirtualHost);
				await Task.Delay(0);
				return "RabbitMQ connection initialized successfully.";
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "RabbitMqService: Ошибка при инициализации подключения");
				throw;
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			if (_persistentConnection != null && _persistentConnection.IsOpen)
			{
				_persistentConnection.Close();
				_persistentConnection.Dispose();
				_persistentConnection = null;
				_logger.LogInformation("RabbitMqService: Подключение успешно закрыто.");
			}
			else
			{
				_logger.LogWarning("RabbitMqService: Подключение уже было закрыто или не было открыто.");
			}

			return Task.CompletedTask;
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
						_logger.LogInformation("RabbitMqService: Подключение к RabbitMQ успешно установлено.");
						return _persistentConnection;
					}
					catch (BrokerUnreachableException ex)
					{
						attempt++;
						_logger.LogWarning(ex, "Попытка {Attempt}/{MaxAttempts}: не удалось подключиться к RabbitMQ.", attempt, MaxRetryAttempts);

						if (attempt == MaxRetryAttempts)
						{
							_logger.LogError("RabbitMqService: Все попытки подключения исчерпаны.");
							throw;
						}

						Task.Delay(RetryDelayMilliseconds).Wait();
					}
				}

				throw new InvalidOperationException("RabbitMqService: Не удалось установить соединение.");
			}
		}

		public async Task PublishMessageAsync(string queueName, string routingKey, string message)
		{
			await Task.Run(() =>
			{
				using var channel = PersistentConnection.CreateModel();

				channel.QueueDeclare(
					queue: queueName,
					durable: true,
					exclusive: false,
					autoDelete: false,
					arguments: null);

				var body = Encoding.UTF8.GetBytes(message);

				var properties = channel.CreateBasicProperties();
				properties.Persistent = true;
				properties.Headers = new Dictionary<string, object>
				{
					["key"] = routingKey
				};

				channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: properties, body: body);

				_logger.LogInformation("RabbitMqService: Сообщение опубликовано в очередь {Queue}", queueName);
			});
		}

		public IConnection CreateConnection() => PersistentConnection;

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
}