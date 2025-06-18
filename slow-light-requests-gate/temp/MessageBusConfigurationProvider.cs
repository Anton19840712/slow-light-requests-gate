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
			// если появятся сценарии для запуска определенного инстанса на старте запуска приложения
			if (jsonDocFromRestRequest == null)
			{
				//берем из конфигурации:
				var busType = _configuration["GateWayType"];
				var busJson = _configuration["MessageBus"];

				if (string.IsNullOrWhiteSpace(busJson))
				{
					throw new InvalidOperationException("MessageBus configuration is missing");
				}
				using var jsonDocWithGateParamsFromConfiuration = JsonDocument.Parse(busJson);

				return ParseConfiguration(jsonDocWithGateParamsFromConfiuration.RootElement, busType);
			}
			else
			{
				// Получаем gateWayType из JSON, пришедшего от REST-запроса
				var root = jsonDocFromRestRequest.RootElement;

				if (!root.TryGetProperty("gateWayType", out var gatewayTypeElement))
				{
					throw new InvalidOperationException("Не удалось найти параметр 'gateWayType' в JSON.");
				}

				var busType = gatewayTypeElement.GetString();

				return ParseConfiguration(root, busType);
			}
		}

		public MessageBusBaseSettings ParseConfiguration(JsonElement root, string typeOfBusToRun)
		{
			return typeOfBusToRun switch
			{
				"activemq" => ParseActiveMqConfig(root),
				"rabbitmq" => ParseRabbitMqConfig(root),
				"kafka-streams" => ParseKafkaConfig(root),
				_ => throw new NotSupportedException($"Unknown bus type: {typeOfBusToRun}")
			};
		}

		private ActiveMqSettings ParseActiveMqConfig(JsonElement root)
		{
			var activeMqSection = root.GetProperty("activeMq");

			return new ActiveMqSettings
			{
				InstanceNetworkGateId = Guid.NewGuid().ToString(),
				TypeToRun = MessageBusType.ActiveMq,
				BrokerUri = activeMqSection.GetProperty("brokerUri").GetString(),
				QueueName = activeMqSection.GetProperty("queueName").GetString()
			};
		}
		private RabbitMqSettings ParseRabbitMqConfig(JsonElement root)
		{
			var rabbitMqSection = root.GetProperty("rabbitMq");

			// тебе можно просто использовать туже модель RabbitConfiguration
			var rabbitMqSettings = new RabbitMqSettings();

			rabbitMqSettings.InstanceNetworkGateId = Guid.NewGuid().ToString();
			rabbitMqSettings.TypeToRun = MessageBusType.RabbitMq;
			rabbitMqSettings.HostName = rabbitMqSection.GetProperty("hostName").GetString();
			rabbitMqSettings.Port = rabbitMqSection.GetProperty("port").GetInt32();
			rabbitMqSettings.UserName = rabbitMqSection.GetProperty("userName").GetString();
			rabbitMqSettings.Password = rabbitMqSection.GetProperty("password").GetString();
			rabbitMqSettings.QueueName = rabbitMqSection.GetProperty("queueName").GetString();
			rabbitMqSettings.VirtualHost = rabbitMqSection.TryGetProperty("virtualHost", out var vhost) ? vhost.GetString() ?? "" : "";
			rabbitMqSettings.Heartbeat = rabbitMqSection.TryGetProperty("heartbeat", out var hb) ? hb.GetString() ?? "" : "";

			return rabbitMqSettings;
		}

		private KafkaStreamsSettings ParseKafkaConfig(JsonElement root)
		{
			var kafkaSection = root.GetProperty("kafka-streams");

			return new KafkaStreamsSettings
			{
				InstanceNetworkGateId = Guid.NewGuid().ToString(),
				TypeToRun = MessageBusType.KafkaStreams,
				BootstrapServers = kafkaSection.GetProperty("bootstrapServers").GetString(),
				ApplicationId = kafkaSection.GetProperty("applicationId").GetString(),
				InputTopic = kafkaSection.GetProperty("inputTopic").GetString(),
				OutputTopic = kafkaSection.GetProperty("outputTopic").GetString()
			};
		}
	}
}
