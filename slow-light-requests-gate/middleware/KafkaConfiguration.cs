using lazy_light_requests_gate.buses;
using lazy_light_requests_gate.configurationsettings;
using lazy_light_requests_gate.settings;
using Microsoft.Extensions.Options;
using Serilog;

namespace lazy_light_requests_gate.middleware
{
	public static class KafkaConfiguration
	{
		public static IServiceCollection AddKafkaServices(this IServiceCollection services, IConfiguration configuration)
		{
			// Регистрируем настройки
			services.Configure<KafkaSettings>(configuration.GetSection("KafkaSettings"));

			// Регистрируем Kafka сервисы
			services.AddSingleton(provider =>
			{
				var kafkaSettings = provider.GetRequiredService<IOptions<KafkaSettings>>().Value;

				if (string.IsNullOrWhiteSpace(kafkaSettings.BootstrapServers))
				{
					throw new InvalidOperationException("Некорректные настройки Kafka! Проверьте конфигурацию.");
				}

				Log.Information("Kafka настроен: {BootstrapServers}", kafkaSettings.BootstrapServers);
				return kafkaSettings;
			});

			// Здесь будут добавлены сервисы для работы с Kafka
			services.AddSingleton<KafkaService>();

			return services;
		}
	}
}
