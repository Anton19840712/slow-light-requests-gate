using lazy_light_requests_gate.settings;
using System.Text.Json;

namespace lazy_light_requests_gate.temp
{
	public class MessageBusConfigurationProvider : IMessageBusConfigurationProvider
	{
		private readonly IConfiguration _configuration;
		private readonly ILogger<MessageBusConfigurationProvider> _logger;

		public MessageBusConfigurationProvider(IConfiguration configuration, ILogger<MessageBusConfigurationProvider> logger)
		{
			_configuration = configuration;
			_logger = logger;
		}

		public MessageBusBaseSettings GetConfiguration(JsonDocument jsonDocFromRestRequest = null)
		{
			if (jsonDocFromRestRequest == null)
			{
				// Конфигурация берется из appsettings/config.json
				var busType = _configuration["Bus"]?.ToLowerInvariant();

				if (string.IsNullOrWhiteSpace(busType))
					throw new InvalidOperationException("Тип шины 'Bus' не указан в конфигурации.");

				var busSection = _configuration.GetSection("BusSettings");

				if (!busSection.Exists())
					throw new InvalidOperationException("Секция 'BusSettings' не найдена в конфигурации.");

				// Парсим в зависимости от типа шины
				return busType switch
				{
					"rabbit" or "rabbitmq" =>
						Setup(busSection.Get<RabbitMqSettings>(), MessageBusType.RabbitMq),

					"kafka" or "kafka-streams" =>
						Setup(busSection.Get<KafkaStreamsSettings>(), MessageBusType.KafkaStreams),

					"activemq" =>
						Setup(busSection.Get<ActiveMqSettings>(), MessageBusType.ActiveMq),

					_ => throw new NotSupportedException($"Тип шины '{busType}' не поддерживается.")
				};

				MessageBusBaseSettings Setup(MessageBusBaseSettings settings, MessageBusType type)
				{
					settings.TypeToRun = type;
					settings.InstanceNetworkGateId = Guid.NewGuid().ToString();
					return settings;
				}
			}
			else
			{
				// Конфигурация пришла по REST
				var root = jsonDocFromRestRequest.RootElement;

				if (!root.TryGetProperty("gateWayType", out var gatewayTypeElement))
					throw new InvalidOperationException("Не удалось найти параметр 'gateWayType' в JSON.");

				var busType = gatewayTypeElement.GetString()?.ToLowerInvariant();

				return ParseConfiguration(root, busType);
			}
		}

		public MessageBusBaseSettings ParseConfiguration(JsonElement root, string typeOfBusToRun)
		{
			return typeOfBusToRun switch
			{
				"activemq" => ParseActiveMqConfig(root),
				"rabbitmq" or "rabbit" => ParseRabbitMqConfig(root),
				"kafka-streams" or "kafka" => ParseKafkaConfig(root),
				_ => throw new NotSupportedException($"Unknown bus type: {typeOfBusToRun}")
			};
		}

		private ActiveMqSettings ParseActiveMqConfig(JsonElement root)
		{
			var section = root.GetProperty("activeMq");

			return new ActiveMqSettings
			{
				InstanceNetworkGateId = Guid.NewGuid().ToString(),
				TypeToRun = MessageBusType.ActiveMq,
				BrokerUri = section.GetProperty("brokerUri").GetString(),
				QueueName = section.GetProperty("queueName").GetString()
			};
		}

		private RabbitMqSettings ParseRabbitMqConfig(JsonElement root)
		{
			var section = root.GetProperty("rabbitMq");

			var settings = new RabbitMqSettings();

			settings.InstanceNetworkGateId = Guid.NewGuid().ToString();
			settings.TypeToRun = MessageBusType.RabbitMq;
			settings.HostName = section.GetProperty("hostName").GetString();
			settings.Port = section.GetProperty("port").GetInt32();
			settings.UserName = section.GetProperty("userName").GetString();
			settings.Password = section.GetProperty("password").GetString();
			settings.PushQueueName = section.TryGetProperty("pushQueueName", out var pushQueue) ? pushQueue.GetString() ?? "" : "";
			settings.ListenQueueName = section.TryGetProperty("listenQueueName", out var listenQueue) ? listenQueue.GetString() ?? "" : "";
			settings.VirtualHost = section.TryGetProperty("virtualHost", out var vhost) ? vhost.GetString() ?? "" : "";
			settings.Heartbeat = section.TryGetProperty("heartbeat", out var hb) ? hb.GetString() ?? "" : "";

			return settings;
		}

		private KafkaStreamsSettings ParseKafkaConfig(JsonElement root)
		{
			var section = root.GetProperty("kafka-streams");

			return new KafkaStreamsSettings
			{
				InstanceNetworkGateId = Guid.NewGuid().ToString(),
				TypeToRun = MessageBusType.KafkaStreams,
				BootstrapServers = section.GetProperty("bootstrapServers").GetString(),
				ApplicationId = section.GetProperty("applicationId").GetString(),
				InputTopic = section.GetProperty("inputTopic").GetString(),
				OutputTopic = section.GetProperty("outputTopic").GetString()
			};
		}
	}
}
