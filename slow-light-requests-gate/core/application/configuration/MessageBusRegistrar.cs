using lazy_light_requests_gate.core.application.interfaces.buses;
using lazy_light_requests_gate.infrastructure.configuration;
using lazy_light_requests_gate.infrastructure.services.buses;

namespace lazy_light_requests_gate.core.application.configuration
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
			services.AddRabbitMqServices(configuration);
		}

		private void RegisterActiveMq(IServiceCollection services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется в DI ActiveMQ");
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
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется в DI Kafka");
			services.AddSingleton<IKafkaStreamsService, KafkaStreamsService>();
		}

		private void RegisterPulsar(IServiceCollection services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется в DI Pulsar");
			services.AddSingleton<IPulsarService, PulsarService>();
		}

		private void RegisterTarantool(IServiceCollection services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется в DI Tarantool");
			services.AddSingleton<ITarantoolService, TarantoolService>();
		}
	}
}
