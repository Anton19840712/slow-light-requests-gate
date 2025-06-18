
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
			services.AddSingleton<IMessageBrokerFactory, MessageBrokerFactory>();

			// НЕ регистрируем IMessageBrokerService здесь - будем получать через фабрику напрямую

			return services;
		}
	}
}