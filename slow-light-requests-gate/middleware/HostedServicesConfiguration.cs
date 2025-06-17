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
				services.AddHostedService<QueueListenerRabbitPostgresBackgroundService>();
				services.AddHostedService<OutboxPostgresBackgroundService>();
			}
			else // mongo по умолчанию
			{
				services.AddHostedService<QueueListenerRabbitMongoBackgroundService>();
				services.AddHostedService<OutboxMongoBackgroundService>();
			}

			return services;
		}
	}
}
