namespace lazy_light_requests_gate.middleware
{
	static class HostedServicesConfiguration
	{
		/// <summary>
		/// Регистрация фоновых сервисов приложения.
		/// </summary>
		public static IServiceCollection AddHostedServices(this IServiceCollection services)
		{
			//services.AddHostedService<QueueListenerRabbitMongoBackgroundService>();
			//services.AddHostedService<OutboxMongoBackgroundService>();

			services.AddHostedService<QueueListenerRabbitPostgresBackgroundService>();
			services.AddHostedService<OutboxPostgresBackgroundService>();

			return services;
		}
	}
}
