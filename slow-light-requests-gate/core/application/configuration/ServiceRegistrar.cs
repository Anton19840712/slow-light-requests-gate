using lazy_light_requests_gate.core.application.interfaces.buses;
using lazy_light_requests_gate.core.application.interfaces.databases;
using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.core.application.interfaces.runtime;
using lazy_light_requests_gate.core.application.services.buses;
using lazy_light_requests_gate.core.application.services.databases;
using lazy_light_requests_gate.core.application.services.messageprocessing;
using lazy_light_requests_gate.core.application.services.runtime;
using lazy_light_requests_gate.infrastructure.configuration;
using Microsoft.OpenApi.Models;

namespace lazy_light_requests_gate.core.application.configuration;

public class ServiceRegistrar
{
	public void ConfigureServices(WebApplicationBuilder builder, string instanceId)
	{
		var configuration = builder.Configuration;
		var services = builder.Services;

		// Регистрируем ApplicationTypeService
		services.AddSingleton<IApplicationTypeService, ApplicationTypeService>();

		var selectedDatabase = configuration["Database"]?.ToLower();
		var selectedBus = configuration["Bus"]?.ToLower();
		var applicationType = configuration["ApplicationType"]?.ToLowerInvariant() ?? "restonly";

		LogConfigurationInfo(selectedDatabase, selectedBus);

		RegisterBaseServices(services, instanceId);
		RegisterCommonServices(services, configuration);
		RegisterDatabaseServices(services, configuration, selectedDatabase);
		RegisterMessageBusServices(services, configuration, selectedBus);
		RegisterHostedServices(services, configuration, selectedDatabase);
		RegisterApplicationServices(services);
		RegisterNetworkServices(services);
		RegisterApplicationServices(services);
	}

	private void RegisterBaseServices(IServiceCollection services, string instanceId)
	{
		services.AddSingleton(instanceId);
		services.AddSingleton<IApplicationTypeService, ApplicationTypeService>();

		// ВАЖНО: Правильная регистрация контроллеров
		services.AddControllers()
			.AddJsonOptions(options =>
			{
				options.JsonSerializerOptions.PropertyNamingPolicy = null; // Сохраняем исходные имена
			});

		// ВАЖНО: Правильная регистрация Swagger
		services.AddEndpointsApiExplorer();
		services.AddSwaggerGen(c =>
		{
			c.SwaggerDoc("v1", new OpenApiInfo
			{
				Title = "Dynamic Gate API",
				Version = "v1",
				Description = "API для динамического шлюза запросов"
			});

			// Добавляем поддержку авторизации в Swagger (если нужно)
			c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
			{
				Type = SecuritySchemeType.Http,
				Scheme = "bearer",
				BearerFormat = "JWT",
				Description = "Введите JWT токен"
			});
		});
	}

	private void RegisterCommonServices(IServiceCollection services, IConfiguration configuration)
	{
		try
		{
			services.AddHttpServices();
			services.AddHeadersServices();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[{GetTimestamp()}] [ERROR] Ошибка регистрации общих сервисов: {ex.Message}");
			throw;
		}
	}

	private void RegisterDatabaseServices(IServiceCollection services, IConfiguration configuration, string selectedDatabase)
	{
		try
		{
			services.AddMongoDbServices(configuration);
			services.AddMessageServingServices(configuration);
			services.AddPostgresDbServices(configuration);
			services.AddPostgresDbRepositoriesServices(configuration);
			services.AddMessageServingServices(configuration);
			services.AddSingleton<IDynamicDatabaseManager, DynamicDatabaseManager>();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[{GetTimestamp()}] [ERROR] Ошибка регистрации сервисов БД: {ex.Message}");
			throw;
		}
	}

	private void RegisterMessageBusServices(IServiceCollection services, IConfiguration configuration, string selectedBus)
	{
		try
		{
			var busRegistrar = new MessageBusRegistrar();
			busRegistrar.RegisterBusService(services, configuration, selectedBus);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[{GetTimestamp()}] [ERROR] Ошибка регистрации шины сообщений: {ex.Message}");
			throw;
		}
	}

	private void RegisterHostedServices(IServiceCollection services, IConfiguration configuration, string selectedDatabase)
	{
		try
		{
			if (selectedDatabase == "postgres" || selectedDatabase == "mongo")
			{
				services.AddHostedServices(configuration);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[{GetTimestamp()}] [ERROR] Ошибка регистрации hosted сервисов: {ex.Message}");
		}
	}

	private void RegisterApplicationServices(IServiceCollection services)
	{
		try
		{
			services.AddSingleton<IMessageProcessingServiceFactory, MessageProcessingServiceFactory>();
			services.AddSingleton<IMessageBusServiceFactory, MessageBusServiceFactory>();
			services.AddAuthentication();
			services.AddAuthorization();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[{GetTimestamp()}] [ERROR] Ошибка регистрации сервисов приложения: {ex.Message}");
			throw;
		}
	}

	private void RegisterNetworkServices(IServiceCollection services)
	{
		try
		{
			services.AddNetworkServices();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[{GetTimestamp()}] [ERROR] Ошибка регистрации сервисов network: {ex.Message}");
			throw;
		}
	}

	private void LogConfigurationInfo(string selectedDatabase, string selectedBus)
	{
		var timestamp = GetTimestamp();
	}

	private string GetTimestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
}
