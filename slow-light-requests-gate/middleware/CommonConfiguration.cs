using lazy_light_requests_gate.services.lazy_light_requests_gate.services;
using lazy_light_requests_gate.services;

namespace lazy_light_requests_gate.middleware
{
	static class CommonConfiguration
	{
		/// <summary>
		/// Регистрация сервисов общего назначения.
		/// </summary>
		public static IServiceCollection AddCommonServices(this IServiceCollection services)
		{
			services.AddCors();
			services.AddScoped<ICleanupService, CleanupService>();
			services.AddScoped<IIncidentCleanupService, IncidentCleanupService>();
			return services;
		}
	}
}
