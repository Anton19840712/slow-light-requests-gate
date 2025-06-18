using Apache.NMS;
using Apache.NMS.Util;
using lazy_light_requests_gate.temp;
using System.Collections.Concurrent;
using ISession = Apache.NMS.ISession;

namespace infrastructure.messaging
{
	public class ActiveMqService : IMessageBusService, IDisposable
	{
		private readonly ILogger<ActiveMqService> _logger;
		private string _defaultQueueName;
		private IConnection _connection;
		private ISession _session;
		private bool _disposed = false;

		private readonly ConcurrentDictionary<string, IMessageProducer> _producers = new();
		private readonly ConcurrentDictionary<string, IDestination> _destinations = new();

		public ActiveMqService(ILogger<ActiveMqService> logger)
		{
			_logger = logger;
		}

		public string TransportName => "kafkastreams";
		public Task<string> StartAsync(MessageBusBaseSettings config, CancellationToken cancellationToken)
		{
			if (config is not ActiveMqSettings activeMqConfig)
				throw new ArgumentException("Неверный тип конфигурации для ActiveMQ");

			var brokerUri = activeMqConfig.BrokerUri ?? "activemq:tcp://localhost:61616";
			_defaultQueueName = activeMqConfig.QueueName ?? "test-queue";

			InitializeConnection(brokerUri);

			return Task.FromResult(_connection.ClientId);
		}

		public Task PublishMessageAsync(string topic, string key, string message)
		{
			try
			{
				var producer = GetOrCreateProducer(topic);
				var textMessage = _session.CreateTextMessage(message);
				textMessage.Properties.SetString("key", key);

				producer.Send(textMessage);
				_logger?.LogInformation("Сообщение отправлено в очередь {Topic}", topic);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Ошибка при публикации сообщения в очередь {Topic}", topic);
			}

			return Task.CompletedTask;
		}

		public async Task<string> WaitForResponseAsync(string topic, int timeoutMilliseconds = 15000, CancellationToken cancellationToken = default)
		{
			try
			{
				var destination = GetOrCreateDestination(topic);
				using var consumer = _session.CreateConsumer(destination);

				_logger?.LogInformation("Ожидание сообщения из очереди {Topic} в течение {Timeout} мс", topic, timeoutMilliseconds);

				var receiveTask = Task.Run(() => consumer.Receive(TimeSpan.FromMilliseconds(timeoutMilliseconds)), cancellationToken);
				var message = await receiveTask;

				if (message is ITextMessage textMessage)
				{
					_logger?.LogInformation("Сообщение получено из очереди {Topic}", topic);
					return textMessage.Text;
				}

				_logger?.LogWarning("Сообщение из {Topic} не получено или оно не является текстовым", topic);
				return null;
			}
			catch (OperationCanceledException)
			{
				_logger?.LogWarning("Ожидание было отменено для очереди {Topic}", topic);
				return null;
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Ошибка при ожидании ответа из очереди {Topic}", topic);
				return null;
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			Dispose();
			return Task.CompletedTask;
		}

		private void InitializeConnection(string brokerUri)
		{
			try
			{
				var factory = new NMSConnectionFactory(brokerUri);
				_connection = factory.CreateConnection();

				//setting connection name:
				_connection.ClientId = $"client-{Guid.NewGuid()}";
				_connection.Start();

				_session = _connection.CreateSession(AcknowledgementMode.AutoAcknowledge);
				_logger?.LogInformation("ActiveMqService: подключение к брокеру по адресу {BrokerUri} установлено, Connection c сlientId={ClientId}, Oчередь: {Queue}",
					brokerUri, _connection.ClientId, _defaultQueueName);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Ошибка при инициализации соединения с брокером {BrokerUri}", brokerUri);
				throw;
			}
		}

		private IDestination GetOrCreateDestination(string topic)
		{
			return _destinations.GetOrAdd(topic, t => SessionUtil.GetDestination(_session, $"queue://{t}"));
		}

		private IMessageProducer GetOrCreateProducer(string topic)
		{
			return _producers.GetOrAdd(topic, t =>
			{
				var destination = GetOrCreateDestination(t);
				return _session.CreateProducer(destination);
			});
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			try
			{
				foreach (var producer in _producers.Values)
				{
					producer?.Close();
				}

				_session?.Close();
				_connection?.Close();
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Ошибка при освобождении ресурсов ActiveMqService");
			}

			GC.SuppressFinalize(this);
		}
	}
}
