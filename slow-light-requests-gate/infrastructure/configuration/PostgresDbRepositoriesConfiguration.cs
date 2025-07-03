using lazy_light_requests_gate.core.application.interfaces.repos;
using lazy_light_requests_gate.infrastructure.data.repos;

namespace lazy_light_requests_gate.infrastructure.configuration
{
	public static class PostgresDbRepositoriesConfiguration
	{
		public static IServiceCollection AddPostgresDbRepositoriesServices(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddScoped(typeof(IPostgresRepository<>), typeof(PostgresRepository<>));
			return services;
		}
	}
}
