using lazy_light_requests_gate.core.application.interfaces.repos;
using lazy_light_requests_gate.infrastructure.data.repos;

namespace lazy_light_requests_gate.infrastructure.configuration
{
	public static class MongoDbRepositoriesConfiguration
	{
		public static IServiceCollection AddMongoDbRepositoriesServices(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));
			return services;
		}
	}
}