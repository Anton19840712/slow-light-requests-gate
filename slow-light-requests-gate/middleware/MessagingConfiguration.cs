using lazy_light_requests_gate.processing;

namespace lazy_light_requests_gate.middleware
{
	static class MessagingConfiguration
	{
		/// <summary>
		/// Регистрация сервисов, участвующих в отсылке и получении сообщений.
		/// </summary>
		public static IServiceCollection AddMessageServingServices(this IServiceCollection services)
		{
			services.AddTransient<IMessageProcessingService, MessageProcessingService>();

			return services;
		}
	}
}
