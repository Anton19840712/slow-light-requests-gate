using application.interfaces.services;
using infrastructure.messaging;
using infrastructure.services.formatting;
using infrastructure.services.processing;
using infrastructure.temp;

namespace infrastructure.configuration
{
	static class MessagingConfiguration
	{
		/// <summary>
		/// Регистрация сервисов, участвующих в отсылке и получении сообщений.
		/// </summary>
		public static IServiceCollection AddMessageServingServices(this IServiceCollection services)
		{
			services.AddScoped<ConnectionMessageSenderFactory>();
			services.AddScoped<IMessageSender, MessageSender>();
			services.AddTransient<IMessageProcessingService, MessageProcessingService>();

			return services;
		}
	}
}
