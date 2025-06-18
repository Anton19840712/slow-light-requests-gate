using Confluent.Kafka;
using lazy_light_requests_gate.messaging;
using lazy_light_requests_gate.settings;
using Microsoft.Extensions.Options;

namespace lazy_light_requests_gate.buses
{
	public class KafkaService : IMessageBrokerService, IDisposable
	{
		private readonly KafkaSettings _kafkaSettings;
		private readonly ILogger<KafkaService> _logger;
		private readonly IProducer<Null, string> _producer;
		private readonly ConsumerConfig _consumerConfig;

		public KafkaService(
			IOptions<KafkaSettings> kafkaSettings,
			ILogger<KafkaService> logger)
		{
			_kafkaSettings = kafkaSettings.Value;
			_logger = logger;

			var producerConfig = new ProducerConfig
			{
				BootstrapServers = _kafkaSettings.BootstrapServers,
				SecurityProtocol = Enum.Parse<SecurityProtocol>(_kafkaSettings.SecurityProtocol),
			};

			if (!string.IsNullOrEmpty(_kafkaSettings.SaslUsername))
			{
				producerConfig.SaslMechanism = Enum.Parse<SaslMechanism>(_kafkaSettings.SaslMechanism);
				producerConfig.SaslUsername = _kafkaSettings.SaslUsername;
				producerConfig.SaslPassword = _kafkaSettings.SaslPassword;
			}

			_producer = new ProducerBuilder<Null, string>(producerConfig).Build();

			_consumerConfig = new ConsumerConfig
			{
				BootstrapServers = _kafkaSettings.BootstrapServers,
				GroupId = _kafkaSettings.GroupId,
				SecurityProtocol = Enum.Parse<SecurityProtocol>(_kafkaSettings.SecurityProtocol),
				SessionTimeoutMs = _kafkaSettings.SessionTimeoutMs,
				EnableAutoCommit = _kafkaSettings.EnableAutoCommit,
				AutoOffsetReset = AutoOffsetReset.Earliest
			};

			if (!string.IsNullOrEmpty(_kafkaSettings.SaslUsername))
			{
				_consumerConfig.SaslMechanism = Enum.Parse<SaslMechanism>(_kafkaSettings.SaslMechanism);
				_consumerConfig.SaslUsername = _kafkaSettings.SaslUsername;
				_consumerConfig.SaslPassword = _kafkaSettings.SaslPassword;
			}

			_logger.LogInformation("Kafka service initialized with servers: {BootstrapServers}", _kafkaSettings.BootstrapServers);
		}

		public async Task PublishMessageAsync(string topic, string routingKey, string message)
		{
			try
			{
				var result = await _producer.ProduceAsync(topic, new Message<Null, string> { Value = message });
				_logger.LogInformation("Message published to Kafka topic {Topic} at offset {Offset}", topic, result.Offset);
			}
			catch (ProduceException<Null, string> ex)
			{
				_logger.LogError(ex, "Failed to publish message to Kafka topic {Topic}", topic);
				throw;
			}
		}

		public async Task<string> WaitForResponseAsync(string topic, int timeoutMilliseconds = 15000)
		{
			using var consumer = new ConsumerBuilder<Null, string>(_consumerConfig).Build();
			consumer.Subscribe(topic);

			var cancellationTokenSource = new CancellationTokenSource(timeoutMilliseconds);

			try
			{
				while (!cancellationTokenSource.Token.IsCancellationRequested)
				{
					var consumeResult = consumer.Consume(cancellationTokenSource.Token);
					if (consumeResult?.Message?.Value != null)
					{
						_logger.LogInformation("Received message from Kafka topic {Topic}", topic);
						return consumeResult.Message.Value;
					}
				}
			}
			catch (OperationCanceledException)
			{
				_logger.LogWarning("Timeout waiting for response from Kafka topic {Topic}", topic);
			}
			catch (ConsumeException ex)
			{
				_logger.LogError(ex, "Error consuming from Kafka topic {Topic}", topic);
			}

			return null;
		}

		public string GetBrokerType() => "kafka";

		public void Dispose()
		{
			_producer?.Dispose();
		}
	}
}
