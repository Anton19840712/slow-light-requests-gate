using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization;
using lazy_light_requests_gate.core.application.interfaces.repos;
using lazy_light_requests_gate.infrastructure.data.repos;
using lazy_light_requests_gate.core.application.interfaces.databases;
using lazy_light_requests_gate.core.domain.settings.databases;
using lazy_light_requests_gate.infrastructure.services.databases;

namespace lazy_light_requests_gate.infrastructure.configuration
{
	static class MongoDbConfiguration
	{
		public static IServiceCollection AddMongoDbServices(this IServiceCollection services, IConfiguration configuration)
		{
			var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

			var mongoSettings = configuration.GetSection("MongoDbSettings");
			services.Configure<MongoDbSettings>(mongoSettings);

			// Настраиваем сериализацию Guid-ов
			BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;

			try
			{
				BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
			}
			catch (BsonSerializationException)
			{
				// Сериализатор уже зарегистрирован, игнорируем
				Console.WriteLine($"[{timestamp}] [DEBUG] GUID сериализатор уже зарегистрирован");
			}

			// ГЛАВНОЕ ИЗМЕНЕНИЕ: Регистрируем только динамический клиент
			services.AddSingleton<IDynamicMongoClient, DynamicMongoClient>();
			services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));
			return services;
		}
	}
}
