using lazy_light_requests_gate.entities;
using lazy_light_requests_gate.background;
using lazy_light_requests_gate.repositories;

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
				services.AddHostedService<OutboxBackgroundServiceBase<IPostgresRepository<OutboxMessage>>>();
			}
			else // mongo по умолчанию
			{
				services.AddHostedService<QueueListenerBackgroundServiceBase<IMongoRepository<QueuesEntity>>>();
				services.AddHostedService<OutboxBackgroundServiceBase<IMongoRepository<OutboxMessage>>>();
			}

			return services;
		}
	}
}