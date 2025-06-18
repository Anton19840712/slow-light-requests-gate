using Confluent.Kafka;
using lazy_light_requests_gate.temp;
using Streamiz.Kafka.Net;
using Streamiz.Kafka.Net.SerDes;

namespace infrastructure.messaging
{
	public class KafkaStreamsService : IMessageBusService, IDisposable
	{
		private KafkaStream _stream;
		private IProducer<Null, string> _producer;
		private readonly IConfiguration _configuration;
		private readonly ILogger<KafkaStreamsService> _logger;
		private string _inputTopic;
		private string _outputTopic;
		private string _bootstrapServers;

		public KafkaStreamsService(IConfiguration configuration, ILogger<KafkaStreamsService> logger)
		{
			_configuration = configuration;
			_logger = logger;
		}
		public string TransportName => "kafka";
		public async Task<string> StartAsync(MessageBusBaseSettings config, CancellationToken cancellationToken)
		{
			string applicationId;

			if (config == null)
			{ 
				_bootstrapServers = _configuration["KafkaStreams:BootstrapServers"] ?? "localhost:9092";
				applicationId = _configuration["KafkaStreams:ApplicationId"] ?? "performance-test-app";
				_inputTopic = _configuration["KafkaStreams:InputTopic"] ?? "input-topic";
				_outputTopic = _configuration["KafkaStreams:OutputTopic"] ?? "output-topic";
			}
			else
			{
				if (config is not KafkaStreamsSettings kafkaConfig)
					throw new ArgumentException("Invalid config type");

				_bootstrapServers = kafkaConfig.BootstrapServers?.ToString() ?? "localhost:9092";
				applicationId = kafkaConfig.ApplicationId.ToString() ?? "performance-test-app";
				_inputTopic = kafkaConfig.InputTopic.ToString() ?? "input-topic";
				_outputTopic = kafkaConfig.OutputTopic.ToString() ?? "output-topic";
			}

			ConfigureStream(applicationId);
			await _stream.StartAsync(cancellationToken);

			var producerConfig = new ProducerConfig { BootstrapServers = _bootstrapServers };
			_producer = new ProducerBuilder<Null, string>(producerConfig).Build();

			return default;
		}

		private void ConfigureStream(string applicationId)
		{
			var config = new StreamConfig<StringSerDes, StringSerDes>
			{
				ApplicationId = applicationId,
				BootstrapServers = _bootstrapServers,
				AutoOffsetReset = AutoOffsetReset.Earliest
			};

			var builder = new StreamBuilder();
			builder.Stream<string, string>(_inputTopic)
				   .To(_outputTopic);

			var topology = builder.Build();
			_stream = new KafkaStream(topology, config);
		}

		public async Task PublishMessageAsync(string topic, string key, string payload)
		{
			try
			{
				var message = new Message<Null, string> { Value = payload };
				var result = await _producer.ProduceAsync(topic, message);
				_logger.LogInformation("KafkaStreamsService: сообщение успешно опубликовано в топик {Topic} с offset {Offset}", topic, result.Offset);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "KafkaStreamsService: ошибка при публикации сообщения в топик {Topic}", topic);
			}
		}

		public async Task<string> WaitForResponseAsync(string topic, int timeoutMilliseconds = 15000, CancellationToken cancellationToken = default)
		{
			var consumerConfig = new ConsumerConfig
			{
				BootstrapServers = _bootstrapServers,
				GroupId = $"wait-response-{Guid.NewGuid()}",
				AutoOffsetReset = AutoOffsetReset.Earliest,
				EnableAutoCommit = false
			};

			using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
			consumer.Subscribe(topic);

			using var cts = new CancellationTokenSource(timeoutMilliseconds);

			try
			{
				_logger.LogInformation($"KafkaStreamsService: ожидаем сообщение из топика {topic} с таймаутом {timeoutMilliseconds}мс...");
				var result = consumer.Consume(cts.Token);
				if (result != null && result.Message != null)
				{
					_logger.LogInformation("KafkaStreamsService: получено сообщение.");
					return result.Message.Value;
				}
			}
			catch (ConsumeException ex)
			{
				_logger.LogError($"KafkaStreamsService: ошибка при получении сообщения: {ex.Error.Reason}");
			}
			catch (OperationCanceledException)
			{
				_logger.LogWarning("KafkaStreamsService: таймаут ожидания сообщения.");
			}

			return null;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			try
			{
				_logger.LogInformation("KafkaStreamsService: остановка потока Kafka...");
				_stream?.Dispose();
				_producer?.Dispose();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "KafkaStreamsService: ошибка при остановке потока Kafka.");
			}

			return Task.CompletedTask;
		}

		public void Dispose()
		{
			_stream?.Dispose();
			_producer?.Dispose();
		}		
	}
}
