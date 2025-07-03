using Confluent.Kafka;
using lazy_light_requests_gate.core.application.interfaces.buses;
using Streamiz.Kafka.Net;
using Streamiz.Kafka.Net.SerDes;

namespace lazy_light_requests_gate.core.application.services.buses
{
	/// <summary>
	/// Сервис для работы с Kafka Streams - следует паттерну других сервисов
	/// </summary>
	public class KafkaStreamsService : IKafkaStreamsService, IDisposable
	{
		private readonly ILogger<KafkaStreamsService> _logger;
		private readonly IConfiguration _configuration;
		private KafkaStream _stream;
		private IProducer<Null, string> _producer;
		private readonly string _bootstrapServers;
		private readonly string _applicationId;
		private readonly string _clientId;
		private readonly string _inputTopic;
		private readonly string _outputTopic;
		private readonly string _groupId;
		private bool _disposed = false;

		public KafkaStreamsService(IConfiguration configuration, ILogger<KafkaStreamsService> logger)
		{
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));

			// Читаем конфигурацию как в других сервисах
			_bootstrapServers = _configuration["KafkaStreamsSettings:BootstrapServers"] ?? "localhost:9092";
			_applicationId = _configuration["KafkaStreamsSettings:ApplicationId"] ?? "gateway-app";
			_clientId = _configuration["KafkaStreamsSettings:ClientId"] ?? "gateway-client";
			_inputTopic = _configuration["KafkaStreamsSettings:InputTopic"] ?? "messages_in";
			_outputTopic = _configuration["KafkaStreamsSettings:OutputTopic"] ?? "messages_out";
			_groupId = _configuration["KafkaStreamsSettings:GroupId"] ?? "gateway-group";

			_logger.LogInformation("KafkaStreamsService initialized with servers: {BootstrapServers}, App ID: {ApplicationId}",
				_bootstrapServers, _applicationId);

			InitializeKafkaComponents();
		}

		private void InitializeKafkaComponents()
		{
			try
			{
				// Настройка Kafka Streams
				var streamConfig = new StreamConfig<StringSerDes, StringSerDes>
				{
					ApplicationId = _applicationId,
					BootstrapServers = _bootstrapServers,
					AutoOffsetReset = AutoOffsetReset.Earliest,
					ClientId = _clientId
				};

				var builder = new StreamBuilder();

				// Создаем поток: входной топик -> выходной топик (как в других сервисах)
				builder.Stream<string, string>(_inputTopic)
					   .To(_outputTopic);

				var topology = builder.Build();
				_stream = new KafkaStream(topology, streamConfig);

				// Настройка Producer
				var producerConfig = new ProducerConfig
				{
					BootstrapServers = _bootstrapServers,
					ClientId = _clientId
				};
				_producer = new ProducerBuilder<Null, string>(producerConfig).Build();

				_logger.LogInformation("Kafka Streams components initialized successfully");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to initialize Kafka Streams components");
				throw;
			}
		}

		public async Task PublishMessageAsync(string queueName, string routingKey, string message)
		{
			// Делегируем к перегрузке с одним топиком
			await PublishMessageAsync(queueName, message);
		}

		public async Task PublishMessageAsync(string topic, string message)
		{
			try
			{
				if (_disposed)
				{
					throw new ObjectDisposedException(nameof(KafkaStreamsService));
				}

				var kafkaMessage = new Message<Null, string> { Value = message };
				var result = await _producer.ProduceAsync(topic, kafkaMessage);

				if (result.Status == PersistenceStatus.Persisted)
				{
					_logger.LogDebug("Message published to Kafka topic: {Topic}, Offset: {Offset}",
						topic, result.Offset);
				}
				else
				{
					_logger.LogWarning("Message may not be persisted to Kafka topic: {Topic}, Status: {Status}",
						topic, result.Status);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error publishing message to Kafka topic: {Topic}", topic);
				throw;
			}
		}

		public async Task StartListeningAsync(string queueName, CancellationToken cancellationToken)
		{
			try
			{
				if (_disposed)
				{
					throw new ObjectDisposedException(nameof(KafkaStreamsService));
				}

				_logger.LogInformation("Starting Kafka Streams listener for topic: {Topic}", queueName);
				await StartAsync(cancellationToken);  // Добавить await здесь
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error starting Kafka Streams listener for topic: {Topic}", queueName);
				throw;
			}
		}

		public Task<bool> TestConnectionAsync()
		{
			try
			{
				_logger.LogDebug("Testing Kafka connection...");

				// Создаем временный AdminClient для проверки подключения
				using var adminClient = new AdminClientBuilder(new AdminClientConfig
				{
					BootstrapServers = _bootstrapServers
				}).Build();

				// Пытаемся получить метаданные - это проверит подключение
				var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));

				_logger.LogInformation("Kafka connection test successful. Brokers: {BrokerCount}, Topics: {TopicCount}",
					metadata.Brokers.Count, metadata.Topics.Count);

				return Task.FromResult(true);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Kafka connection test failed");
				return Task.FromResult(false);
			}
		}

		public string GetBusType()
		{
			return "kafkastreams";
		}

		public Task StartAsync(CancellationToken cancellationToken = default)
		{
			try
			{
				if (_disposed)
				{
					throw new ObjectDisposedException(nameof(KafkaStreamsService));
				}

				_logger.LogInformation("Starting Kafka Streams application: {ApplicationId}", _applicationId);
				return _stream.StartAsync(cancellationToken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to start Kafka Streams application");
				throw;
			}
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				_logger.LogInformation("Disposing KafkaStreamsService");

				try
				{
					_stream?.Dispose();
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error disposing Kafka Stream");
				}

				try
				{
					_producer?.Dispose();
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error disposing Kafka Producer");
				}

				_disposed = true;
				_logger.LogInformation("KafkaStreamsService disposed successfully");
			}
		}
	}
}
