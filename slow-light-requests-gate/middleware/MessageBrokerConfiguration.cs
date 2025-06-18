
using lazy_light_requests_gate.buses;
using lazy_light_requests_gate.messaging;
using lazy_light_requests_gate.rabbitqueuesconnections;

namespace lazy_light_requests_gate.middleware
{
	public static class MessageBrokerConfiguration
	{
		public static IServiceCollection AddMessageBrokerServices(this IServiceCollection services, IConfiguration configuration)
		{
			var messageBroker = configuration["MessageBroker"]?.ToString()?.ToLower() ?? "rabbitmq";

			// Регистрируем фабрику
			services.AddScoped<IMessageBrokerFactory, MessageBrokerFactory>();

			// Регистрируем основной сервис на основе конфигурации
			if (messageBroker == "kafka")
			{
				services.AddTransient<IMessageBrokerService>(provider =>
					provider.GetRequiredService<KafkaService>());
			}
			else
			{
				services.AddTransient<IMessageBrokerService>(provider =>
					provider.GetRequiredService<RabbitMqService>());
			}

			return services;
		}
	}
}
