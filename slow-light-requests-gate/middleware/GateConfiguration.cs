using Newtonsoft.Json.Linq;

namespace lazy_light_requests_gate.middleware;

/// <summary>
/// Класс используется для предоставления возможности настройщику системы
/// динамически задавать хост и порт самого динамического шлюза.
/// </summary>
public class GateConfiguration
{
	/// <summary>
	/// Настройка динамических параметров шлюза и возврат HTTP/HTTPS адресов
	/// </summary>
	public async Task<(string HttpUrl, string HttpsUrl)> ConfigureDynamicGateAsync(string[] args, WebApplicationBuilder builder)
	{
		var configFilePath = args.FirstOrDefault(a => a.StartsWith("--config="))?.Substring(9) ?? "./configs/rest.json";
		var config = LoadConfiguration(configFilePath);

		var configType = config["type"]?.ToString() ?? config["Type"]?.ToString();
		return await ConfigureRestGate(config, builder);
	}

	private async Task<(string HttpUrl, string HttpsUrl)> ConfigureRestGate(JObject config, WebApplicationBuilder builder)
	{
		var companyName = config["CompanyName"]?.ToString() ?? "default-company";
		var host = config["Host"]?.ToString() ?? "localhost";
		var port = int.TryParse(config["Port"]?.ToString(), out var p) ? p : 5000;
		var enableValidation = bool.TryParse(config["Validate"]?.ToString(), out var v) && v;

		builder.Configuration["CompanyName"] = companyName;
		builder.Configuration["Host"] = host;
		builder.Configuration["Port"] = port.ToString();
		builder.Configuration["Validate"] = enableValidation.ToString();

		var httpUrl = $"http://{host}:80";
		var httpsUrl = $"https://{host}:443";
		return await Task.FromResult((httpUrl, httpsUrl));
	}

	private static JObject LoadConfiguration(string configFilePath)
	{
		try
		{
			string fullPath;

			if (Path.IsPathRooted(configFilePath))
			{
				fullPath = configFilePath;
			}
			else
			{
				var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
				fullPath = Path.GetFullPath(Path.Combine(basePath, configFilePath));
			}

			// Печатаем информацию для отладки.
			Console.WriteLine();
			Console.WriteLine($"[INFO] Конечный путь к конфигу: {fullPath}");
			Console.WriteLine($"[INFO] Загружается конфигурация: {Path.GetFileName(fullPath)}");
			Console.WriteLine();

			// Проверка существования файла.
			if (!File.Exists(fullPath))
				throw new FileNotFoundException("Файл конфигурации не найден", fullPath);

			// Загружаем конфигурацию из файла.
			var json = File.ReadAllText(fullPath);
			return JObject.Parse(json);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Ошибка при загрузке конфигурации из файла '{configFilePath}': {ex.Message}");
		}
	}
}
