using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.core.application.services.messageprocessing;

namespace lazy_light_requests_gate.infrastructure.configuration
{
	static class MessagingConfiguration
	{
		/// <summary>
		/// Регистрация сервисов, участвующих в отсылке и получении сообщений на основе параметра Database.
		/// </summary>
		public static IServiceCollection AddMessageServingServices(this IServiceCollection services, IConfiguration configuration)
		{
			var database = configuration["Database"]?.ToString()?.ToLower() ?? "mongo";
			var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрация MessageProcessing сервисов для Database='{database}'");

			// Регистрируем фабрику (всегда нужна)
			services.AddScoped<IMessageProcessingServiceFactory, MessageProcessingServiceFactory>();

			// УСЛОВНАЯ регистрация сервисов в зависимости от выбранной базы данных

			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется MessageProcessingPostgresService");
			services.AddTransient<MessageProcessingPostgresService>();
			services.AddTransient<IMessageProcessingService, MessageProcessingPostgresService>();
			
			Console.WriteLine($"[{timestamp}] [CONFIG] Регистрируется MessageProcessingMongoService");
			services.AddTransient<MessageProcessingMongoService>();
			services.AddTransient<IMessageProcessingService, MessageProcessingMongoService>();

			return services;
		}
	}
}
