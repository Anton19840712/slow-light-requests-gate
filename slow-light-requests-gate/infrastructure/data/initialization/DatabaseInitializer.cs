using lazy_light_requests_gate.infrastructure.configuration;

namespace lazy_light_requests_gate.infrastructure.data.initialization
{
	public class DatabaseInitializer
	{
		public async Task InitializeDatabaseAsync(IConfiguration configuration, string selectedDatabase)
		{
			var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

			switch (selectedDatabase)
			{
				case "postgres":
					Console.WriteLine($"[{timestamp}] [INIT] Инициализация PostgreSQL...");
					await PostgresDbConfiguration.EnsureDatabaseInitializedAsync(configuration);
					await PostgresDbConfiguration.DiagnoseDatabaseAsync(configuration);
					Console.WriteLine($"[{timestamp}] [INIT] PostgreSQL инициализирован успешно");
					break;

				case "mongo":
					Console.WriteLine($"[{timestamp}] [INIT] Инициализация MongoDB...");
					// TODO: Добавить инициализацию MongoDB если потребуется
					Console.WriteLine($"[{timestamp}] [INIT] MongoDB готов к работе");
					break;

				default:
					throw new InvalidOperationException($"Неподдерживаемая база данных: {selectedDatabase}");
			}
		}
	}
}
