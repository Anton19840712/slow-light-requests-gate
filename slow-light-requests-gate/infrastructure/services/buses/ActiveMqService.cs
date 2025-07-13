using Apache.NMS;
using lazy_light_requests_gate.core.application.interfaces.buses;

namespace lazy_light_requests_gate.infrastructure.services.buses
{
	/// <summary>
	/// Исправленный ActiveMQ сервис с правильным транспортом
	/// </summary>
	public class ActiveMqService : IActiveMqService, IDisposable
	{
		private readonly ILogger<ActiveMqService> _logger;
		private readonly string _brokerUri;
		private IConnection _connection;
		private Apache.NMS.ISession _session;
		private readonly object _lockObject = new object();
		private bool _disposed = false;

		public ActiveMqService(string brokerUri, ILogger<ActiveMqService> logger = null)
		{
			_logger = logger;
			try
			{
				// запускается на старте - этого делать не нужно, если установлен rabbit
				_brokerUri = brokerUri;
				var factory = new NMSConnectionFactory("tcp://localhost:61616");
				_connection = factory.CreateConnection();
				_connection.Start();
				_session = _connection.CreateSession(AcknowledgementMode.AutoAcknowledge);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Failed to initialize ActiveMQ connection with URI: {BrokerUri}", brokerUri);
				throw;
			}
		}

		private void EnsureConnection()
		{
			if (_connection == null || !_connection.IsStarted)
			{
				try
				{
					_session?.Close();
					_connection?.Close();

					var factory = new NMSConnectionFactory(_brokerUri);
					_connection = factory.CreateConnection();
					_connection.Start();
					_session = _connection.CreateSession(AcknowledgementMode.AutoAcknowledge);

					_logger?.LogDebug("ActiveMQ connection established successfully");
				}
				catch (Exception ex)
				{
					_logger?.LogError(ex, "Failed to establish ActiveMQ connection to {BrokerUri}", _brokerUri);
					throw;
				}
			}
		}

		public async Task PublishMessageAsync(string queueName, string message)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(ActiveMqService));

			try
			{
				_logger?.LogDebug("Publishing message to ActiveMQ queue: {QueueName}", queueName);

				await Task.Run(() =>
				{
					lock (_lockObject)
					{
						EnsureConnection();

						var destination = _session.GetQueue(queueName);
						using var producer = _session.CreateProducer(destination);

						var textMessage = _session.CreateTextMessage(message);
						producer.Send(textMessage);
					}
				});

				_logger?.LogInformation("Message published to ActiveMQ queue: {QueueName}", queueName);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Error publishing message to ActiveMQ queue: {QueueName}", queueName);
				throw;
			}
		}

		public Task PublishMessageAsync(string queueName, string routingKey, string message)
		{
			// В ActiveMQ routing key обычно не используется, так что можно игнорировать
			return PublishMessageAsync(queueName, message);
		}

		public async Task StartListeningAsync(string queueName, CancellationToken cancellationToken)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(ActiveMqService));

			try
			{
				_logger?.LogInformation("Starting ActiveMQ listener for queue: {QueueName}", queueName);

				var factory = new NMSConnectionFactory(_brokerUri);
				using var connection = factory.CreateConnection();
				connection.Start();

				using var session = connection.CreateSession(AcknowledgementMode.AutoAcknowledge);
				var destination = session.GetQueue(queueName);
				using var consumer = session.CreateConsumer(destination);

				consumer.Listener += (message) =>
				{
					try
					{
						if (message is ITextMessage textMessage)
						{
							_logger?.LogInformation("Received message from ActiveMQ queue {QueueName}: {Message}",
								queueName, textMessage.Text);
						}
						else
						{
							_logger?.LogWarning("Received non-text message from ActiveMQ queue {QueueName}", queueName);
						}
					}
					catch (Exception ex)
					{
						_logger?.LogError(ex, "Error processing message from ActiveMQ queue {QueueName}", queueName);
					}
				};

				_logger?.LogInformation("ActiveMQ listener started for queue: {QueueName}", queueName);

				while (!cancellationToken.IsCancellationRequested)
				{
					await Task.Delay(1000, cancellationToken);
				}

				_logger?.LogInformation("ActiveMQ listener stopped for queue: {QueueName}", queueName);
			}
			catch (OperationCanceledException)
			{
				_logger?.LogInformation("ActiveMQ listener cancelled for queue: {QueueName}", queueName);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Error starting ActiveMQ listener for queue: {QueueName}", queueName);
				throw;
			}
		}

		public async Task<bool> TestConnectionAsync()
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(ActiveMqService));

			try
			{
				return await Task.Run(() =>
				{
					var factory = new NMSConnectionFactory("tcp://localhost:61616");
					using var connection = factory.CreateConnection();
					connection.Start();
					using var session = connection.CreateSession(AcknowledgementMode.AutoAcknowledge);
					var destination = session.GetQueue("test-connection-queue");
					using var producer = session.CreateProducer(destination);

					_logger?.LogDebug("ActiveMQ connection test successful");
					return true;
				});
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "ActiveMQ connection test failed for {BrokerUri}", _brokerUri);
				return false;
			}
		}

		public string GetBusType()
		{
			return "activemq";
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				lock (_lockObject)
				{
					try
					{
						_session?.Close();
						_connection?.Close();
						_logger?.LogDebug("ActiveMQ connection disposed");
					}
					catch (Exception ex)
					{
						_logger?.LogWarning(ex, "Error disposing ActiveMQ connection");
					}
					finally
					{
						_disposed = true;
					}
				}
			}
		}
	}
}