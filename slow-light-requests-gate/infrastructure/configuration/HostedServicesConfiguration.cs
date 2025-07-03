using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.core.application.services.messageprocessing;
using lazy_light_requests_gate.infrastructure.background;

namespace lazy_light_requests_gate.infrastructure.configuration
{
	static class HostedServicesConfiguration
	{
		/// <summary>
		/// Регистрация фоновых сервисов приложения на основе параметра Database.
		/// </summary>
		public static IServiceCollection AddHostedServices(this IServiceCollection services, IConfiguration configuration)
		{
			var database = configuration["Database"]?.ToString()?.ToLower() ?? "mongo";

			// Регистрируем обычные сервисы для возможности их получения через DI
			services.AddHostedService<QueueListenerRabbitBackgroundService>();

			// Регистрируем динамические фоновые сервисы
			services.AddHostedService<DynamicOutboxCleanupService>();
			services.AddHostedService<DynamicIncidentCleanupService>();

			return services;
		}
	}
}
