using infrastructure.services.background;
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
			// Регистрируем динамические фоновые сервисы
			services.AddHostedService<DynamicOutboxCleanupService>();
			services.AddHostedService<DynamicIncidentCleanupService>();
			services.AddHostedService<NetworkServerHostedService>();
			
			return services;
		}
	}
}
