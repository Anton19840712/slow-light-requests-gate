using lazy_light_requests_gate.core.application.interfaces.buses;
using lazy_light_requests_gate.core.application.services.buses;
using lazy_light_requests_gate.infrastructure.configuration;

namespace lazy_light_requests_gate.infrastructure.startup
{
	public class MessageBusRegistrar
	{
		public void RegisterBusService(IServiceCollection services, IConfiguration configuration, string selectedBus)
		{
			var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
			// Регистрация IDynamicBusManager
			services.AddScoped<IDynamicBusManager, DynamicBusManager>();

			switch (selectedBus)
			{
				case "rabbit":
					RegisterRabbitMq(services, configuration, timestamp);
					break;
				case "activemq":
					RegisterActiveMq(services, configuration, timestamp);
					break;
				case "kafka":
					RegisterKafka(services, configuration, timestamp);
					break;
				case "pulsar":
					RegisterPulsar(services, configuration, timestamp);
					break;
				case "tarantool":
					RegisterTarantool(services, configuration, timestamp);
					break;
				default:
					throw new InvalidOperationException($"Неподдерживаемая шина сообщений: {selectedBus}. Поддерживаются: rabbit, activemq, kafka, pulsar, tarantool");
			}
		}

		private void RegisterRabbitMq(IServiceCollection services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется RabbitMQ");
			Console.WriteLine("=== ДОБАВЛЯЕМ RABBITMQ СЕРВИСЫ ===");
			Console.WriteLine($"RabbitMQ Host из конфигурации: {configuration["RabbitMqSettings:HostName"]}");
			Console.WriteLine($"RabbitMQ Port из конфигурации: {configuration["RabbitMqSettings:Port"]}");
			Console.WriteLine($"RabbitMQ User из конфигурации: {configuration["RabbitMqSettings:UserName"]}");
			Console.WriteLine($"RabbitMQ VHost из конфигурации: {configuration["RabbitMqSettings:VirtualHost"]}");
			Console.WriteLine("================================");
			services.AddRabbitMqServices(configuration);
		}

		private void RegisterActiveMq(IServiceCollection services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется ActiveMQ");
			services.AddSingleton<IActiveMqService>(sp =>
			{
				var serviceUrl = configuration["ActiveMqSettings:BrokerUri"] ?? "tcp://localhost:61616";
				var logger = sp.GetRequiredService<ILogger<ActiveMqService>>();
				Console.WriteLine($"[{timestamp}] [CONFIG] ActiveMQ BrokerUri: {serviceUrl}");
				return new ActiveMqService(serviceUrl, logger);
			});
		}

		private void RegisterKafka(IServiceCollection services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется Kafka");
			services.AddSingleton<IKafkaStreamsService, KafkaStreamsService>();
		}

		private void RegisterPulsar(IServiceCollection services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется Pulsar");
			services.AddSingleton<IPulsarService, PulsarService>();
		}

		private void RegisterTarantool(IServiceCollection services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется Tarantool");
			services.AddSingleton<ITarantoolService, TarantoolService>();
		}
	}
}
