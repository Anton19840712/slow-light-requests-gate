using lazy_light_requests_gate.configurationsettings;
using MongoDB.Driver;
using System.Security.Authentication;

namespace lazy_light_requests_gate.middleware
{
	static class MongoDbConfiguration
	{
		/// <summary>
		/// Регистрация MongoDB клиента и логики работы с базой.
		/// </summary>
		public static IServiceCollection AddMongoDbServices(this IServiceCollection services, IConfiguration configuration)
		{
			var mongoSettings = configuration.GetSection("MongoDbSettings");
			services.Configure<MongoDbSettings>(configuration.GetSection("MongoDbSettings"));

			var user = mongoSettings.GetValue<string>("User");
			var password = mongoSettings.GetValue<string>("Password");
			var connectionString = mongoSettings.GetValue<string>("ConnectionString");
			var databaseName = mongoSettings.GetValue<string>("DatabaseName");

			var mongoUrl = new MongoUrlBuilder(connectionString)
			{
				Username = user,
				Password = password
			}.ToString();

			var settings = MongoClientSettings.FromUrl(new MongoUrl(mongoUrl));
			settings.SslSettings = new SslSettings { EnabledSslProtocols = SslProtocols.Tls12 };

			// Регистрируем MongoClient один раз
			services.AddSingleton<IMongoClient>(new MongoClient(settings));

			// Регистрируем IMongoDatabase один раз
			services.AddSingleton(sp =>
			{
				var client = sp.GetRequiredService<IMongoClient>();
				return client.GetDatabase(databaseName);
			});

			return services;
		}
	}
}
