using lazy_light_requests_gate.background;
using lazy_light_requests_gate.services;

namespace lazy_light_requests_gate.middleware
{
	static class HostedServicesConfiguration
	{
		/// <summary>
		/// Регистрация фоновых сервисов приложения на основе параметра Database.
		/// </summary>
		public static IServiceCollection AddHostedServices(this IServiceCollection services, IConfiguration configuration)
		{
			var database = configuration["Database"]?.ToString()?.ToLower() ?? "mongo";

			if (database == "postgres")
			{
				services.AddScoped(typeof(ICleanupService<>), typeof(CleanupService<>));
				services.AddHostedService<QueueListenerRabbitPostgresBackgroundService>();
				services.AddHostedService<OutboxBackgroundServicePostgres>();
			}
			else // mongo по умолчанию
			{
				services.AddScoped(typeof(ICleanupService<>), typeof(CleanupService<>));
				services.AddHostedService<QueueListenerBackgroundServiceMongo>();
				services.AddHostedService<OutboxBackgroundServiceMongo>();
			}

			return services;
		}
	}
}