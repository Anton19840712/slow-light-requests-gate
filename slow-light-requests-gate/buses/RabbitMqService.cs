using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;
using lazy_light_requests_gate.messaging;
using lazy_light_requests_gate.temp;

namespace lazy_light_requests_gate.buses
{
	public class RabbitMqService : IRabbitMqService, IMessageBrokerService, IDisposable
	{
		private readonly IConnectionFactory _factory;
		private readonly ILogger<RabbitMqService> _logger;
		private IConnection _persistentConnection;

		public RabbitMqService(ILogger<RabbitMqService> logger, IConnectionFactory factory)
		{
			_logger = logger;
			_factory = factory; // Теперь используем factory из DI-контейнера
		}

		// Свойство для получения постоянного соединения
		private IConnection PersistentConnection
		{
			get
			{
				if (_persistentConnection != null)
					return _persistentConnection;
				// TODO: вынести в конфигурацию
				var attempt = 0;
				var maxAttempts = 5;
				var delayMs = 3000;

				while (attempt < maxAttempts)
				{
					try
					{
						_persistentConnection = _factory.CreateConnection();

						_logger.LogInformation("RabbitMqService: попытка установления подключения к сетевой шине успешна.");
						return _persistentConnection;
					}
					catch (BrokerUnreachableException ex)
					{
						attempt++;
						_logger.LogWarning($"Попытка {attempt}/{maxAttempts}: не удалось подключиться к RabbitMQ ({ex.Message}).");

						if (attempt == maxAttempts)
						{
							_logger.LogError("Исчерпаны все попытки подключения к RabbitMQ. Ты че, docker не запустил?");
							throw;
						}

						Thread.Sleep(delayMs);
					}
				}

				throw new InvalidOperationException("Не удалось установить соединение с RabbitMQ.");
			}
		}

		public string TransportName => throw new NotImplementedException();

		// Метод для публикации сообщений
		public async Task PublishMessageAsync(
			string queueName,
			string routingKey,
			string message)
		{
			await Task.Run(() =>
			{
				using var channel = PersistentConnection.CreateModel();

				// Очередь теперь постоянная
				channel.QueueDeclare(
					queue: queueName,
					durable: true,
					exclusive: false,
					autoDelete: false,
					arguments: null);

				var body = Encoding.UTF8.GetBytes(message);

				// Сообщение теперь тоже персистентное
				var properties = channel.CreateBasicProperties();
				properties.Persistent = true;

				channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: properties, body: body);
			});
		}

		// Метод для возврата существующего соединения
		public IConnection CreateConnection()
		{
			return PersistentConnection;
		}

		public string GetBrokerType()
		{
			return "rabbitmq";
		}

		/// <summary>
		/// Ожидание ответа от удаленной очереди в течение указанного времени.
		/// </summary>
		public async Task<string> WaitForResponseAsync(string queueName, int timeoutMilliseconds = 15000)
		{
			using var channel = PersistentConnection.CreateModel();
			channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

			var consumer = new EventingBasicConsumer(channel);
			var completionSource = new TaskCompletionSource<string>();

			consumer.Received += (model, ea) =>
			{
				var response = Encoding.UTF8.GetString(ea.Body.ToArray());
				completionSource.SetResult(response);
			};

			channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);

			var completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(timeoutMilliseconds));
			return completedTask == completionSource.Task ? completionSource.Task.Result : null;
		}

		public Task<string> StartAsync(MessageBusBaseSettings config, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<string> WaitForResponseAsync(string topic, int timeoutMilliseconds = 15000, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			throw new NotImplementedException();
		}
	}
}