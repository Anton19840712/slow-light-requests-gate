namespace lazy_light_requests_gate.middleware
{
	static class HostedServicesConfiguration
	{
		/// <summary>
		/// Регистрация фоновых сервисов приложения.
		/// </summary>
		public static IServiceCollection AddHostedServices(this IServiceCollection services)
		{
			services.AddHostedService<QueueListenerBackgroundService>();
			services.AddHostedService<OutboxMongoBackgroundService>();

			return services;
		}
	}
}
