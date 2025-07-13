using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;

namespace lazy_light_requests_gate.core.application.services.buses
{
	public class RabbitMqBusService : IRabbitMqBusService, IDisposable
	{
		private readonly IConnectionFactory _factory;
		private readonly ILogger<RabbitMqBusService> _logger;
		private IConnection _persistentConnection;
		private bool _disposed = false;

		public RabbitMqBusService(IConnectionFactory factory, ILogger<RabbitMqBusService> logger=null)
		{
			_logger = logger;
			_factory = factory;

			// Логируем информацию о factory при создании
			_logger.LogInformation("RabbitMqBusService создан. Factory URI: {Uri}", factory.Uri?.ToString() ?? "NULL");
		}

		// Свойство для получения постоянного соединения
		private IConnection PersistentConnection
		{
			get
			{
				if (_persistentConnection != null && _persistentConnection.IsOpen)
				{
					_logger.LogDebug("Используется существующее соединение с RabbitMQ");
					return _persistentConnection;
				}

				_logger.LogInformation("Создаем новое соединение с RabbitMQ...");

				var attempt = 0;
				var maxAttempts = 5;
				var delayMs = 3000;

				while (attempt < maxAttempts)
				{
					try
					{
						attempt++;
						_logger.LogInformation("Попытка подключения {Attempt}/{MaxAttempts} к RabbitMQ", attempt, maxAttempts);

						_persistentConnection?.Dispose();
						_persistentConnection = _factory.CreateConnection();

						_logger.LogInformation("RabbitMQ соединение установлено успешно. Endpoint: {Endpoint}",
							_persistentConnection.Endpoint?.ToString() ?? "Unknown");
						return _persistentConnection;
					}
					catch (BrokerUnreachableException ex)
					{
						_logger.LogWarning("Попытка {Attempt}/{MaxAttempts}: не удалось подключиться к RabbitMQ. Ошибка: {Error}",
							attempt, maxAttempts, ex.Message);

						if (attempt == maxAttempts)
						{
							_logger.LogError(ex, "Исчерпаны все попытки подключения к RabbitMQ. Factory URI: {Uri}",
								_factory.Uri?.ToString() ?? "NULL");
							throw;
						}

						Thread.Sleep(delayMs);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Неожиданная ошибка при подключении к RabbitMQ на попытке {Attempt}/{MaxAttempts}",
							attempt, maxAttempts);

						if (attempt == maxAttempts)
							throw;

						Thread.Sleep(delayMs);
					}
				}

				throw new InvalidOperationException("Не удалось установить соединение с RabbitMQ.");
			}
		}

		// Метод для публикации сообщений
		public async Task PublishMessageAsync(string queueName, string routingKey, string message)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(RabbitMqBusService));

			_logger.LogInformation("Начинаем публикацию сообщения в очередь {QueueName} с routing key {RoutingKey}",
				queueName, routingKey ?? "NULL");

			try
			{
				await Task.Run(() =>
				{
					_logger.LogDebug("Получаем соединение для публикации...");
					var connection = PersistentConnection;

					_logger.LogDebug("Создаем канал...");
					using var channel = connection.CreateModel();

					_logger.LogDebug("Канал создан. Объявляем очередь {QueueName}...", queueName);

					// Очередь теперь постоянная
					// Происходит декларация очереди tomsk_out:
					var queueDeclareResult = channel.QueueDeclare(
						queue: queueName,
						durable: true,
						exclusive: false,
						autoDelete: false,
						arguments: null);

					_logger.LogInformation("Очередь {QueueName} объявлена. Consumers: {ConsumerCount}, Messages: {MessageCount}",
						queueName, queueDeclareResult.ConsumerCount, queueDeclareResult.MessageCount);

					var body = Encoding.UTF8.GetBytes(message);
					_logger.LogDebug("Сообщение закодировано. Размер: {Size} байт", body.Length);

					// Сообщение теперь тоже персистентное
					var properties = channel.CreateBasicProperties();
					properties.Persistent = true;
					properties.MessageId = Guid.NewGuid().ToString();
					properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

					_logger.LogInformation("Публикуем сообщение с ID {MessageId} в очередь {QueueName}", properties.MessageId, queueName);

					channel.BasicPublish(
						exchange: "",
						routingKey: queueName,
						basicProperties: properties,
						body: body);

					_logger.LogInformation("Сообщение успешно опубликовано в очередь {QueueName}. MessageId: {MessageId}",
						queueName, properties.MessageId);
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Ошибка при публикации сообщения в очередь {QueueName}: {ErrorMessage}",
					queueName, ex.Message);
				throw;
			}
		}

		// Метод для возврата существующего соединения
		public IConnection CreateConnection()
		{
			return PersistentConnection;
		}

		// Ожидание ответа с таймаутом, если ответ не получен, соединение прекращается
		public async Task<string> WaitForResponseAsync(string queueName, int timeoutMilliseconds = 15000)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(RabbitMqBusService));

			_logger.LogInformation("Ожидание ответа из очереди {QueueName} с таймаутом {Timeout}мс", queueName, timeoutMilliseconds);

			try
			{
				using var channel = PersistentConnection.CreateModel();

				// происходит декларация очереди с другим значением из конфигурации:
				channel.QueueDeclare(
						queue: queueName,
						durable: false,
						exclusive: false,
						autoDelete: false,
						arguments: null);

				var consumer = new EventingBasicConsumer(channel);
				var completionSource = new TaskCompletionSource<string>();

				consumer.Received += (model, ea) =>
				{
					var response = Encoding.UTF8.GetString(ea.Body.ToArray());
					_logger.LogInformation("Получен ответ из очереди {QueueName}: {Response}", queueName, response);
					completionSource.SetResult(response);
				};

				channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);

				var completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(timeoutMilliseconds));

				if (completedTask == completionSource.Task)
				{
					return completionSource.Task.Result;
				}
				else
				{
					_logger.LogWarning("Таймаут при ожидании ответа из очереди {QueueName}", queueName);
					return null;
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Ошибка при ожидании ответа из очереди {QueueName}", queueName);
				throw;
			}
		}

		public async Task StartListeningAsync(string queueName, CancellationToken cancellationToken)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(RabbitMqBusService));

			try
			{
				_logger.LogInformation("Запуск прослушивания очереди RabbitMQ: {QueueName}", queueName);

				using var channel = PersistentConnection.CreateModel();

				var queueDeclareResult = channel.QueueDeclare(
					queue: queueName,
					durable: true,
					exclusive: false,
					autoDelete: false,
					arguments: null);

				_logger.LogInformation("Очередь {QueueName} для прослушивания готова. Messages: {MessageCount}",
					queueName, queueDeclareResult.MessageCount);

				var consumer = new EventingBasicConsumer(channel);

				consumer.Received += (model, ea) =>
				{
					try
					{
						var body = ea.Body.ToArray();
						var message = Encoding.UTF8.GetString(body);

						_logger.LogInformation("Получено сообщение из RabbitMQ очереди {QueueName}: {Message}",
							queueName, message);

						// Подтверждаем получение сообщения
						channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

						_logger.LogDebug("Сообщение подтверждено. DeliveryTag: {DeliveryTag}", ea.DeliveryTag);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Ошибка при обработке сообщения из очереди {QueueName}", queueName);

						// Отклоняем сообщение и возвращаем в очередь
						channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
					}
				};

				channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

				_logger.LogInformation("Прослушивание RabbitMQ очереди {QueueName} запущено", queueName);

				await Task.CompletedTask;
			}
			catch (OperationCanceledException)
			{
				_logger.LogInformation("Прослушивание RabbitMQ очереди {QueueName} отменено", queueName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Ошибка при запуске прослушивания RabbitMQ очереди {QueueName}", queueName);
				throw;
			}
		}

		public async Task<bool> TestConnectionAsync()
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(RabbitMqBusService));

			_logger.LogInformation("Тестирование соединения с RabbitMQ...");

			try
			{
				return await Task.Run(() =>
				{
					var connection = PersistentConnection;
					_logger.LogDebug("Соединение получено для теста");

					using var channel = connection.CreateModel();
					_logger.LogDebug("Канал создан для теста");

					// Создаем тестовую очередь для публикации:
					var testQueue = "test-connection-queue";
					var queueDeclareResult = channel.QueueDeclare(
						queue: testQueue,
						durable: false,
						exclusive: false,
						autoDelete: true,
						arguments: null);

					_logger.LogDebug("Тестовая очередь {TestQueue} создана", testQueue);

					// Отправляем тестовое сообщение:
					var testMessage = $"Test message at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
					var body = Encoding.UTF8.GetBytes(testMessage);

					channel.BasicPublish(exchange: "", routingKey: testQueue, basicProperties: null, body: body);

					_logger.LogInformation("RabbitMQ тест соединения успешен. Тестовое сообщение отправлено в очередь {testQueue} : {TestMessage}", testQueue, testMessage);
					return true;
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "RabbitMQ тест соединения неудачен: {ErrorMessage}", ex.Message);
				return false;
			}
		}

		public string GetBusType()
		{
			return "rabbit";
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				_logger.LogInformation("🔌 Закрытие RabbitMQ соединения...");
				try
				{
					_persistentConnection?.Close();
					_persistentConnection?.Dispose();
					_logger.LogInformation("RabbitMQ соединение закрыто");
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Ошибка при закрытии RabbitMQ соединения");
				}
				finally
				{
					_disposed = true;
				}
			}
		}
	}
}
