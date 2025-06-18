using System.Security.Authentication;
using lazy_light_requests_gate.repositories;
using lazy_light_requests_gate.settings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Serilog;

namespace lazy_light_requests_gate.middleware
{
	static class MongoDbConfiguration
	{
		public static IServiceCollection AddMongoDbServices(this IServiceCollection services, IConfiguration configuration)
		{
			var mongoSettings = configuration.GetSection("MongoDbSettings");
			services.Configure<MongoDbSettings>(mongoSettings);

			var user = mongoSettings.GetValue<string>("User");
			var password = mongoSettings.GetValue<string>("Password");
			var connectionString = mongoSettings.GetValue<string>("ConnectionString");
			var databaseName = mongoSettings.GetValue<string>("DatabaseName");

			var mongoUrlBuilder = new MongoUrlBuilder(connectionString);

			// Устанавливаем учетные данные только если они не пустые
			if (!string.IsNullOrWhiteSpace(user))
			{
				mongoUrlBuilder.Username = user;
			}
			if (!string.IsNullOrWhiteSpace(password))
			{
				mongoUrlBuilder.Password = password;
			}

			var mongoUrl = mongoUrlBuilder.ToString();

			// 🔐 Настраиваем сериализацию Guid-ов
			BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3; // современный режим (рекомендуется)

			try
			{
				BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
			}
			catch (BsonSerializationException)
			{
				// Сериализатор уже зарегистрирован, игнорируем
			}


			var settings = MongoClientSettings.FromUrl(new MongoUrl(mongoUrl));
			settings.SslSettings = new SslSettings { EnabledSslProtocols = SslProtocols.Tls12 };

			// 👇 Регистрируем MongoClient
			services.AddSingleton<IMongoClient>(new MongoClient(settings));

			// 👇 Регистрируем базу
			services.AddSingleton(sp =>
			{
				var client = sp.GetRequiredService<IMongoClient>();
				return client.GetDatabase(databaseName);
			});
			services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));
			//services.AddScoped(typeof(ICleanupService<IMongoRepository<OutboxMessage>>), typeof(CleanupService<IMongoRepository<OutboxMessage>>));
			return services;
		}

		public static async Task EnsureDatabaseInitializedAsync(IConfiguration configuration)
		{
			try
			{
				var mongoSettings = configuration.GetSection("MongoDbSettings");
				var user = mongoSettings.GetValue<string>("User");
				var password = mongoSettings.GetValue<string>("Password");
				var connectionString = mongoSettings.GetValue<string>("ConnectionString");
				var databaseName = mongoSettings.GetValue<string>("DatabaseName");

				var mongoUrlBuilder = new MongoUrlBuilder(connectionString);

				if (!string.IsNullOrWhiteSpace(user))
				{
					mongoUrlBuilder.Username = user;
				}
				if (!string.IsNullOrWhiteSpace(password))
				{
					mongoUrlBuilder.Password = password;
				}

				var mongoUrl = mongoUrlBuilder.ToString();
				var settings = MongoClientSettings.FromUrl(new MongoUrl(mongoUrl));
				settings.SslSettings = new SslSettings { EnabledSslProtocols = SslProtocols.Tls12 };

				var client = new MongoClient(settings);
				var database = client.GetDatabase(databaseName);

				// Получаем названия коллекций из конфигурации
				var collections = mongoSettings.GetSection("Collections");
				var queueCollectionName = collections.GetValue<string>("QueueCollection") ?? "QueueEntities";
				var outboxCollectionName = collections.GetValue<string>("OutboxCollection") ?? "OutboxMessages";
				var incidentCollectionName = collections.GetValue<string>("IncidentCollection") ?? "IncidentEntities";

				// Создаем коллекции если их нет
				var existingCollections = await (await database.ListCollectionNamesAsync()).ToListAsync();

				if (!existingCollections.Contains(queueCollectionName))
				{
					await database.CreateCollectionAsync(queueCollectionName);
					Log.Information("Создана коллекция: {Collection}", queueCollectionName);
				}

				if (!existingCollections.Contains(outboxCollectionName))
				{
					await database.CreateCollectionAsync(outboxCollectionName);
					Log.Information("Создана коллекция: {Collection}", outboxCollectionName);
				}

				if (!existingCollections.Contains(incidentCollectionName))
				{
					await database.CreateCollectionAsync(incidentCollectionName);
					Log.Information("Создана коллекция: {Collection}", incidentCollectionName);
				}

				Log.Information("Инициализация MongoDB завершена успешно");
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Ошибка при инициализации MongoDB");
				throw;
			}
		}
	}
}
