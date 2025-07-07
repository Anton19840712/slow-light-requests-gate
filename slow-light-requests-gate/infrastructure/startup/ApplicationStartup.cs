using lazy_light_requests_gate.infrastructure.configuration;

namespace lazy_light_requests_gate.infrastructure.startup;

public class ApplicationStartup
{
	private readonly ServiceRegistrar _serviceRegistrar;
	private readonly ServiceInitializer _serviceInitializer;
	private readonly ApplicationConfigurator _applicationConfigurator;

	public ApplicationStartup()
	{
		_serviceRegistrar = new ServiceRegistrar();
		_serviceInitializer = new ServiceInitializer();
		_applicationConfigurator = new ApplicationConfigurator();
	}

	public async Task RunAsync(string[] args, string instanceId)
	{
		var builder = WebApplication.CreateBuilder(args);
		LoggingConfiguration.ConfigureLogging(builder);

		// Конфигурация динамического шлюза
		var (httpUrl, httpsUrl) = await GateConfiguration.ConfigureDynamicGateAsync(args, builder);

		// Регистрация сервисов
		_serviceRegistrar.ConfigureServices(builder, instanceId);

		var app = builder.Build();

		// Инициализация выбранных сервисов
		await _serviceInitializer.InitializeSelectedServicesAsync(app);

		// Настройка приложения
		_applicationConfigurator.ConfigureApp(app, httpUrl, httpsUrl);

		// Условная настройка Dapper только для PostgreSQL
		var selectedDatabase = app.Configuration["Database"]?.ToLower();
		if (selectedDatabase == "postgres")
		{
			Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
		}

		await app.RunAsync();
	}
}