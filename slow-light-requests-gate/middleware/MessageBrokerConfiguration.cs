
using lazy_light_requests_gate.buses;
using lazy_light_requests_gate.messaging;
using lazy_light_requests_gate.rabbitqueuesconnections;

namespace lazy_light_requests_gate.middleware
{
	public static class MessageBrokerConfiguration
	{
		public static IServiceCollection AddMessageBrokerServices(this IServiceCollection services, IConfiguration configuration)
		{
			// Регистрируем фабрику
			services.AddScoped<IMessageBrokerFactory, MessageBrokerFactory>();

			// Регистрируем основной сервис через фабрику (динамически)
			services.AddTransient<IMessageBrokerService>(provider =>
			{
				var factory = provider.GetRequiredService<IMessageBrokerFactory>();
				return factory.CreateMessageBroker(factory.GetCurrentBrokerType());
			});

			return services;
		}
	}
}