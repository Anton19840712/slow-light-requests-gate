using lazy_light_requests_gate.core.application.interfaces.buses;
using System.Collections.Concurrent;

namespace lazy_light_requests_gate.infrastructure.services.buses
{
	/// <summary>
	/// Упрощенный менеджер для динамического управления шинами сообщений
	/// </summary>
	public class DynamicBusManager : IDynamicBusManager
	{
		private readonly IMessageBusServiceFactory _messageBusServiceFactory;
		private readonly ILogger<DynamicBusManager> _logger;
		private readonly IConfiguration _configuration;
		private readonly ConcurrentDictionary<string, Dictionary<string, object>> _currentConnections;
		private readonly IServiceScopeFactory _serviceScopeFactory;

		public DynamicBusManager(
			IMessageBusServiceFactory messageBusServiceFactory,
			ILogger<DynamicBusManager> logger,
			IConfiguration configuration,
			IServiceScopeFactory serviceScopeFactory)
		{
			_messageBusServiceFactory = messageBusServiceFactory;
			_logger = logger;
			_configuration = configuration;
			_currentConnections = new ConcurrentDictionary<string, Dictionary<string, object>>();
			_serviceScopeFactory = serviceScopeFactory;
		}

		public async Task ReconnectWithNewParametersAsync(string busType, Dictionary<string, object> connectionParameters)
		{
			try
			{
				_logger.LogInformation("Reconnecting to {BusType} with new parameters", busType);

				// Обновляем конфигурацию
				UpdateConfigurationFromParameters(busType, connectionParameters);

				// Создаем сервис первый раз.
				var dynamicBusService = await CreateBusServiceFromParameters(busType, connectionParameters);
				_messageBusServiceFactory.SetDynamicBusInstance(busType, dynamicBusService);

				// Сохраняем параметры подключения
				_currentConnections[busType] = new Dictionary<string, object>(connectionParameters);

				_logger.LogInformation("Successfully reconnected to {BusType}", busType);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error reconnecting to {BusType}", busType);
				throw;
			}
		}

		public Task<Dictionary<string, object>> GetCurrentConnectionInfoAsync()
		{
			var currentBusType = _messageBusServiceFactory.GetCurrentBusType();

			var connectionInfo = new Dictionary<string, object>
			{
				["currentBusType"] = currentBusType,
				["timestamp"] = DateTime.UtcNow
			};

			if (_currentConnections.TryGetValue(currentBusType, out var currentParams))
			{
				connectionInfo["connectionParameters"] = currentParams;
				connectionInfo["isDynamicConnection"] = true;
			}
			else
			{
				connectionInfo["isDynamicConnection"] = false;
				connectionInfo["message"] = "Using default configuration";
			}

			return Task.FromResult(connectionInfo);
		}

		public void RestoreDefaultConfiguration(string busType)
		{
			_messageBusServiceFactory.RestoreDefaultBusConfiguration(busType);
			_currentConnections.TryRemove(busType, out _);
			_logger.LogInformation("Default configuration restored for {BusType}", busType);
		}

		private void UpdateConfigurationFromParameters(string busType, Dictionary<string, object> parameters)
		{
			var config = (IConfigurationRoot)_configuration;

			switch (busType.ToLower())
			{
				case "rabbit":
					config["RabbitMqSettings:InstanceNetworkGateId"] = GetStringParameter(parameters, "InstanceNetworkGateId", "");
					config["RabbitMqSettings:TypeToRun"] = GetStringParameter(parameters, "TypeToRun", "RabbitMQ");
					config["RabbitMqSettings:HostName"] = GetStringParameter(parameters, "HostName", "localhost");
					config["RabbitMqSettings:Port"] = GetStringParameter(parameters, "Port", "5672");
					config["RabbitMqSettings:UserName"] = GetStringParameter(parameters, "UserName", "guest");
					config["RabbitMqSettings:Password"] = GetStringParameter(parameters, "Password", "guest");
					config["RabbitMqSettings:VirtualHost"] = GetStringParameter(parameters, "VirtualHost", "/");
					config["RabbitMqSettings:InputChannel"] = GetStringParameter(parameters, "InputChannel", "");
					config["RabbitMqSettings:OutputChannel"] = GetStringParameter(parameters, "OutputChannel", "");
					config["RabbitMqSettings:Heartbeat"] = GetStringParameter(parameters, "Heartbeat", "60");
					break;

				case "activemq":
					config["ActiveMqSettings:InstanceNetworkGateId"] = GetStringParameter(parameters, "InstanceNetworkGateId", "");
					config["ActiveMqSettings:TypeToRun"] = GetStringParameter(parameters, "TypeToRun", "ActiveMQ");
					config["ActiveMqSettings:BrokerUri"] = GetStringParameter(parameters, "BrokerUri", "");
					config["ActiveMqSettings:InputChannel"] = GetStringParameter(parameters, "InputChannel", "");
					config["ActiveMqSettings:OutputChannel"] = GetStringParameter(parameters, "OutputChannel", "");
					break;

				case "pulsar":
					config["PulsarSettings:InstanceNetworkGateId"] = GetStringParameter(parameters, "InstanceNetworkGateId", "");
					config["PulsarSettings:TypeToRun"] = GetStringParameter(parameters, "TypeToRun", "Pulsar");
					config["PulsarSettings:ServiceUrl"] = GetStringParameter(parameters, "ServiceUrl", "pulsar://localhost:6650");
					config["PulsarSettings:Tenant"] = GetStringParameter(parameters, "Tenant", "public");
					config["PulsarSettings:Namespace"] = GetStringParameter(parameters, "Namespace", "default");
					config["PulsarSettings:InputTopic"] = GetStringParameter(parameters, "InputTopic", "");
					config["PulsarSettings:OutputTopic"] = GetStringParameter(parameters, "OutputTopic", "");
					config["PulsarSettings:SubscriptionName"] = GetStringParameter(parameters, "SubscriptionName", "default-subscription");
					config["PulsarSettings:SubscriptionType"] = GetStringParameter(parameters, "SubscriptionType", "Exclusive");
					config["PulsarSettings:ConnectionTimeoutSeconds"] = GetStringParameter(parameters, "ConnectionTimeoutSeconds", "15");
					config["PulsarSettings:MaxReconnectAttempts"] = GetStringParameter(parameters, "MaxReconnectAttempts", "3");
					config["PulsarSettings:ReconnectIntervalSeconds"] = GetStringParameter(parameters, "ReconnectIntervalSeconds", "5");
					config["PulsarSettings:EnableCompression"] = GetStringParameter(parameters, "EnableCompression", "false");
					config["PulsarSettings:CompressionType"] = GetStringParameter(parameters, "CompressionType", "LZ4");
					config["PulsarSettings:BatchSize"] = GetStringParameter(parameters, "BatchSize", "1000");
					config["PulsarSettings:BatchingMaxPublishDelayMs"] = GetStringParameter(parameters, "BatchingMaxPublishDelayMs", "10");
					break;

				case "kafkastreams":
					config["KafkaStreamsSettings:InstanceNetworkGateId"] = GetStringParameter(parameters, "InstanceNetworkGateId", "");
					config["KafkaStreamsSettings:TypeToRun"] = GetStringParameter(parameters, "TypeToRun", "KafkaStreams");
					config["KafkaStreamsSettings:BootstrapServers"] = GetStringParameter(parameters, "BootstrapServers", "localhost:9092");
					config["KafkaStreamsSettings:ApplicationId"] = GetStringParameter(parameters, "ApplicationId", "gateway-app");
					config["KafkaStreamsSettings:ClientId"] = GetStringParameter(parameters, "ClientId", "gateway-client");
					config["KafkaStreamsSettings:InputTopic"] = GetStringParameter(parameters, "InputTopic", "messages_in");
					config["KafkaStreamsSettings:OutputTopic"] = GetStringParameter(parameters, "OutputTopic", "messages_out");
					config["KafkaStreamsSettings:GroupId"] = GetStringParameter(parameters, "GroupId", "gateway-group");
					config["KafkaStreamsSettings:AutoOffsetReset"] = GetStringParameter(parameters, "AutoOffsetReset", "earliest");
					config["KafkaStreamsSettings:EnableAutoCommit"] = GetStringParameter(parameters, "EnableAutoCommit", "true");
					config["KafkaStreamsSettings:SessionTimeoutMs"] = GetStringParameter(parameters, "SessionTimeoutMs", "30000");
					config["KafkaStreamsSettings:SecurityProtocol"] = GetStringParameter(parameters, "SecurityProtocol", "PLAINTEXT");
					break;

				case "tarantool":
					config["TarantoolSettings:InstanceNetworkGateId"] = GetStringParameter(parameters, "InstanceNetworkGateId", "");
					config["TarantoolSettings:TypeToRun"] = GetStringParameter(parameters, "TypeToRun", "Tarantool");
					config["TarantoolSettings:Host"] = GetStringParameter(parameters, "Host", "localhost");
					config["TarantoolSettings:Port"] = GetStringParameter(parameters, "Port", "3301");
					config["TarantoolSettings:Username"] = GetStringParameter(parameters, "Username", "");
					config["TarantoolSettings:Password"] = GetStringParameter(parameters, "Password", "");
					config["TarantoolSettings:InputSpace"] = GetStringParameter(parameters, "InputSpace", "messages_in");
					config["TarantoolSettings:OutputSpace"] = GetStringParameter(parameters, "OutputSpace", "messages_out");
					config["TarantoolSettings:StreamName"] = GetStringParameter(parameters, "StreamName", "default-stream");
					break;
			}

			_logger.LogInformation("{BusType} configuration updated in memory", busType);
		}

		// Приватный метод для создания сервиса из параметров
		private Task<IMessageBusService> CreateBusServiceFromParameters(string busType, Dictionary<string, object> parameters)
		{
			var service = busType.ToLower() switch
			{
				"rabbit" => CreateRabbitMqService(),
				"activemq" => CreateActiveMqService(),
				"pulsar" => CreatePulsarService(),
				"kafkastreams" => CreateKafkaService(),
				"tarantool" => CreateTarantoolService(),
				_ => throw new NotSupportedException($"Bus type '{busType}' is not supported")
			};

			return Task.FromResult(service);
		}

		private IMessageBusService CreateRabbitMqService()
		{
			var factory = new RabbitMQ.Client.ConnectionFactory();

			factory.HostName = _configuration["RabbitMqSettings:HostName"];
			factory.Port = int.Parse(_configuration["RabbitMqSettings:Port"] ?? "5672");
			factory.UserName = _configuration["RabbitMqSettings:UserName"];
			factory.Password = _configuration["RabbitMqSettings:Password"];
			factory.VirtualHost = _configuration["RabbitMqSettings:VirtualHost"];
			factory.RequestedHeartbeat = TimeSpan.FromSeconds(int.Parse(_configuration["RabbitMqSettings:Heartbeat"] ?? "60"));
			using var scope = _serviceScopeFactory.CreateScope();
			var logger = scope.ServiceProvider.GetService<ILogger<RabbitMqBusService>>();
			return new RabbitMqBusService(factory, logger);
		}

		private IMessageBusService CreateActiveMqService()
		{
			var brokerUri = _configuration["ActiveMqSettings:BrokerUri"];
			return new ActiveMqService(brokerUri, null);
		}

		private IMessageBusService CreatePulsarService()
		{
			return new PulsarService(_configuration, null);
		}

		private IMessageBusService CreateKafkaService()
		{
			return new KafkaStreamsService(_configuration, null);
		}

		private IMessageBusService CreateTarantoolService()
		{
			return new TarantoolService(_configuration, null);
		}

		// Вспомогательные методы для извлечения параметров
		private string GetStringParameter(Dictionary<string, object> parameters, string key, string defaultValue = null)
		{
			if (parameters.TryGetValue(key, out var value))
			{
				return value?.ToString();
			}
			return defaultValue;
		}
	}
}
