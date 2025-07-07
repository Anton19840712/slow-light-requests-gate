using lazy_light_requests_gate.infrastructure.data.initialization;

namespace lazy_light_requests_gate.core.application.configuration
{
	public class ServiceInitializer
	{
		private readonly DatabaseInitializer _databaseInitializer;
		private readonly MessageBusInitializer _messageBusInitializer;

		public ServiceInitializer()
		{
			_databaseInitializer = new DatabaseInitializer();
			_messageBusInitializer = new MessageBusInitializer();
		}

		public async Task InitializeSelectedServicesAsync(WebApplication app)
		{
			var timestamp = GetTimestamp();
			var selectedDatabase = app.Configuration["Database"]?.ToLower();
			var selectedBus = app.Configuration["Bus"]?.ToLower();

			Console.WriteLine($"[{timestamp}] [INIT] Инициализация выбранных сервисов...");
			Console.WriteLine($"[{timestamp}] [INIT] Database: {selectedDatabase}, Bus: {selectedBus}");

			// Инициализация базы данных
			await _databaseInitializer.InitializeDatabaseAsync(app.Configuration, selectedDatabase);

			// Инициализация шины сообщений
			await _messageBusInitializer.InitializeMessageBusAsync(app.Services, app.Configuration, selectedBus);

			Console.WriteLine($"[{timestamp}] [INIT] Инициализация всех выбранных сервисов завершена успешно");
		}

		private string GetTimestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
	}
}
