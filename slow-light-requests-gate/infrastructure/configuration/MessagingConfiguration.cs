using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.core.application.interfaces.messaging;
using lazy_light_requests_gate.core.application.services.messageprocessing;
using lazy_light_requests_gate.core.application.services.messaging;

namespace lazy_light_requests_gate.infrastructure.configuration
{
	static class MessagingConfiguration
	{
		/// <summary>
		/// Регистрация сервисов, участвующих в отсылке и получении сообщений на основе параметра Database.
		/// </summary>
		public static IServiceCollection AddMessageServingServices(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddScoped<IMessageProcessingServiceFactory, MessageProcessingServiceFactory>();
			services.AddTransient<MessageProcessingPostgresService>();
			services.AddTransient<MessageProcessingMongoService>();

			services.AddScoped<ConnectionMessageSenderFactory>();
			services.AddScoped<IMessageSender, MessageSender>();

			return services;
		}
	}
}
