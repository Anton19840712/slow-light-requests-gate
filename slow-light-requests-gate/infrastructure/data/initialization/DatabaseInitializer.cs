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
					await PostgresDbConfiguration.EnsureDatabaseInitializedAsync(configuration);
					await PostgresDbConfiguration.DiagnoseDatabaseAsync(configuration);
					Console.WriteLine($"[{timestamp}] [INIT] PostgreSQL инициализирован успешно");
					break;

				case "mongo":
					// TODO: Добавить инициализацию MongoDB если потребуется
					break;

				default:
					throw new InvalidOperationException($"Неподдерживаемая база данных: {selectedDatabase}");
			}
		}
	}
}
