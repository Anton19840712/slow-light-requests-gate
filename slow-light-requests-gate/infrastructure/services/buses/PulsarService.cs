using DotPulsar.Abstractions;
using DotPulsar;
using DotPulsar.Extensions;
using System.Buffers;
using lazy_light_requests_gate.core.application.interfaces.buses;

namespace lazy_light_requests_gate.infrastructure.services.buses
{
	/// <summary>
	/// Сервис для работы с Apache Pulsar
	/// </summary>
	public class PulsarService : IPulsarService, IDisposable, IAsyncDisposable
	{
		private readonly ILogger<PulsarService> _logger;
		private readonly IConfiguration _configuration;
		private readonly string _serviceUrl;
		private readonly string _tenant;
		private readonly string _namespace;
		private readonly string _inputTopic;
		private readonly string _outputTopic;
		private readonly string _subscriptionName;
		private IPulsarClient _client;
		private readonly SemaphoreSlim _semaphore = new(1, 1);
		private volatile bool _disposed = false;
		private readonly object _disposeLock = new object();

		public PulsarService(IConfiguration configuration, ILogger<PulsarService> logger = null)
		{
			_logger = logger;
			_configuration = configuration;

			// Читаем настройки прямо из конфигурации
			_serviceUrl = _configuration["PulsarSettings:ServiceUrl"] ?? "pulsar://localhost:6650";
			_tenant = _configuration["PulsarSettings:Tenant"] ?? "public";
			_namespace = _configuration["PulsarSettings:Namespace"] ?? "default";
			_inputTopic = _configuration["PulsarSettings:InputTopic"] ?? "gateway-input";
			_outputTopic = _configuration["PulsarSettings:OutputTopic"] ?? "gateway-output";
			_subscriptionName = _configuration["PulsarSettings:SubscriptionName"] ?? "gateway-subscription";

			_logger?.LogDebug("PulsarService initialized with config: ServiceUrl={ServiceUrl}, InputTopic={InputTopic}, OutputTopic={OutputTopic}, Subscription={Subscription}",
				_serviceUrl, _inputTopic, _outputTopic, _subscriptionName);

			try
			{
				InitializeClient();
				_logger?.LogInformation("Pulsar client initialized successfully for {ServiceUrl}, InputTopic: {InputTopic}, OutputTopic: {OutputTopic}",
					_serviceUrl, _inputTopic, _outputTopic);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Failed to initialize Pulsar client with URL: {ServiceUrl}", _serviceUrl);
				throw;
			}
		}

		private void InitializeClient()
		{
			_client = PulsarClient.Builder()
				.ServiceUrl(new Uri(_serviceUrl))
				.Build();
		}

		private async Task EnsureConnectionAsync()
		{
			if (_client == null || _disposed)
			{
				try
				{
					if (_client != null)
					{
						await _client.DisposeAsync();
					}
					InitializeClient();
					_logger?.LogDebug("Pulsar connection re-established successfully");
				}
				catch (Exception ex)
				{
					_logger?.LogError(ex, "Failed to re-establish Pulsar connection to {ServiceUrl}", _serviceUrl);
					throw;
				}
			}
		}

		public async Task PublishMessageAsync(string topicName, string message)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(PulsarService));

			try
			{
				// Используем InputChannel из конфигурации
				_logger?.LogDebug("Publishing message to Pulsar topic: {TopicName} (configured as OutputTopic)", _inputTopic);

				await _semaphore.WaitAsync();
				try
				{
					await EnsureConnectionAsync();
				}
				finally
				{
					_semaphore.Release();
				}

				var fullTopicName = $"persistent://{_tenant}/{_namespace}/{_inputTopic}";

				await using var producer = _client.NewProducer()
					.Topic(fullTopicName)
					.Create();

				var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
				var messageId = await producer.Send(messageBytes);

				_logger?.LogInformation("Message published to Pulsar topic: {TopicName}, MessageId: {MessageId}",
					_inputTopic, messageId);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Error publishing message to Pulsar topic: {TopicName}", _inputTopic);
				throw;
			}
		}

		public async Task PublishMessageAsync(string topicName, string key, string message)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(PulsarService));

			try
			{
				// Используем OutputChannel из конфигурации
				_logger?.LogDebug("Publishing message with key to Pulsar topic: {TopicName}, Key: {Key}", _inputTopic, key);

				await _semaphore.WaitAsync();
				try
				{
					await EnsureConnectionAsync();
				}
				finally
				{
					_semaphore.Release();
				}

				var fullTopicName = $"persistent://{_tenant}/{_namespace}/{_inputTopic}";

				await using var producer = _client.NewProducer()
					.Topic(fullTopicName)
					.Create();

				var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
				var messageId = await producer.NewMessage()
					.Key(key)
					.Send(messageBytes);

				_logger?.LogInformation("Message with key published to Pulsar topic: {TopicName}, Key: {Key}, MessageId: {MessageId}",
					_outputTopic, key, messageId);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Error publishing message with key to Pulsar topic: {TopicName}", _inputTopic);
				throw;
			}
		}

		public async Task StartListeningAsync(string queueName, CancellationToken cancellationToken)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(PulsarService));

			try
			{
				// Используем InputChannel из конфигурации
				_logger?.LogInformation("Starting Pulsar listener for topic: {TopicName} (configured as InputTopic)", _inputTopic);

				var fullTopicName = $"persistent://{_tenant}/{_namespace}/{_inputTopic}";

				await using var consumer = _client.NewConsumer()
					.Topic(fullTopicName)
					.SubscriptionName(_subscriptionName)
					.SubscriptionType(SubscriptionType.Exclusive)
					.Create();

				_logger?.LogInformation("Pulsar listener started for topic: {TopicName}, Subscription: {SubscriptionName}",
					_inputTopic, _subscriptionName);

				await foreach (var message in consumer.Messages(cancellationToken))
				{
					try
					{
						string messageText;
						if (message.Data.IsSingleSegment)
						{
							messageText = System.Text.Encoding.UTF8.GetString(message.Data.FirstSpan);
						}
						else
						{
							messageText = System.Text.Encoding.UTF8.GetString(message.Data.ToArray());
						}

						_logger?.LogInformation("Received message from Pulsar topic {TopicName}: {Message}, MessageId: {MessageId}",
							_inputTopic, messageText, message.MessageId);

						await consumer.Acknowledge(message.MessageId);
					}
					catch (Exception ex)
					{
						_logger?.LogError(ex, "Error processing message from Pulsar topic {TopicName}, MessageId: {MessageId}",
							_inputTopic, message.MessageId);
					}
				}

				_logger?.LogInformation("Pulsar listener stopped for topic: {TopicName}", _inputTopic);
			}
			catch (OperationCanceledException)
			{
				_logger?.LogInformation("Pulsar listener cancelled for topic: {TopicName}", _inputTopic);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Error starting Pulsar listener for topic: {TopicName}", _inputTopic);
				throw;
			}
		}

		public async Task<bool> TestConnectionAsync()
		{
			_logger?.LogDebug("TestConnectionAsync called. Disposed: {IsDisposed}", _disposed);

			if (_disposed)
			{
				_logger?.LogWarning("Cannot test connection - PulsarService is disposed");
				return false;
			}

			try
			{
				_logger?.LogDebug("Testing Pulsar connection to {ServiceUrl}...", _serviceUrl);

				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

				await _semaphore.WaitAsync(cts.Token);
				try
				{
					await EnsureConnectionAsync();
					_logger?.LogDebug("Client connection ensured");
				}
				finally
				{
					_semaphore.Release();
				}

				// Тестируем реальное подключение отправкой сообщения
				var testTopicName = $"persistent://{_tenant}/{_namespace}/test-connection-{Guid.NewGuid():N}";
				_logger?.LogDebug("Creating producer for test topic: {TopicName}", testTopicName);

				await using var producer = _client.NewProducer()
					.Topic(testTopicName)
					.Create();

				_logger?.LogDebug("Producer created, sending test message...");

				// Отправляем тестовое сообщение для проверки реального подключения
				var testMessage = System.Text.Encoding.UTF8.GetBytes($"test-{DateTime.UtcNow:O}");
				var messageId = await producer.Send(testMessage, cts.Token);

				_logger?.LogInformation("Pulsar connection test successful for {ServiceUrl}, MessageId: {MessageId}",
					_serviceUrl, messageId);
				return true;
			}
			catch (OperationCanceledException)
			{
				_logger?.LogWarning("Pulsar connection test timed out for {ServiceUrl}", _serviceUrl);
				return false;
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Pulsar connection test failed for {ServiceUrl}. Exception: {ExceptionType}, Message: {Message}",
					_serviceUrl, ex.GetType().Name, ex.Message);
				return false;
			}
		}

		public string GetBusType()
		{
			return "pulsar";
		}

		// Синхронный Dispose для совместимости с DI контейнером
		public void Dispose()
		{
			lock (_disposeLock)
			{
				if (_disposed) return;

				try
				{
					_logger?.LogDebug("Starting synchronous dispose of Pulsar client...");

					if (_client != null)
					{
						var disposeTask = _client.DisposeAsync().AsTask();
						disposeTask.Wait(TimeSpan.FromSeconds(5));
					}

					_semaphore?.Dispose();
					_disposed = true;
					_logger?.LogDebug("Pulsar client disposed (sync)");
				}
				catch (Exception ex)
				{
					_logger?.LogWarning(ex, "Error disposing Pulsar client (sync)");
					_disposed = true;
				}
			}
		}

		// Асинхронный Dispose для оптимального освобождения ресурсов
		public async ValueTask DisposeAsync()
		{
			if (_disposed) return;

			lock (_disposeLock)
			{
				if (_disposed) return;
				_disposed = true;
			}

			try
			{
				_logger?.LogDebug("Starting asynchronous dispose of Pulsar client...");

				if (_client != null)
				{
					await _client.DisposeAsync();
				}

				_semaphore?.Dispose();
				_logger?.LogDebug("Pulsar client disposed (async)");
			}
			catch (Exception ex)
			{
				_logger?.LogWarning(ex, "Error disposing Pulsar client (async)");
			}

			GC.SuppressFinalize(this);
		}
	}
}
