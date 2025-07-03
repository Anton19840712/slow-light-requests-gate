using lazy_light_requests_gate.core.application.interfaces.listeners;
using lazy_light_requests_gate.core.application.services.buses;
using lazy_light_requests_gate.core.application.services.listeners;
using RabbitMQ.Client;
using Serilog;

namespace lazy_light_requests_gate.infrastructure.configuration
{
	public static class RabbitConfiguration
	{
		public static IServiceCollection AddRabbitMqServices(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddSingleton<IConnectionFactory>(provider =>
			{
				var config = provider.GetRequiredService<IConfiguration>();

				// Читаем АКТУАЛЬНУЮ конфигурацию (после GateConfiguration)
				var hostName = config["RabbitMqSettings:HostName"];
				var port = int.TryParse(config["RabbitMqSettings:Port"], out var p) ? p : 5672;
				var userName = config["RabbitMqSettings:UserName"];
				var password = config["RabbitMqSettings:Password"];
				var virtualHost = config["RabbitMqSettings:VirtualHost"];

				Console.WriteLine("=== СОЗДАНИЕ RABBITMQ FACTORY ===");
				Console.WriteLine($"Host: {hostName}");
				Console.WriteLine($"Port: {port}");
				Console.WriteLine($"User: {userName}");
				Console.WriteLine($"VHost: '{virtualHost}'");
				Console.WriteLine("================================");

				// Применяем дефолты только если значения пустые
				hostName = string.IsNullOrWhiteSpace(hostName) ? "localhost" : hostName;
				userName = string.IsNullOrWhiteSpace(userName) ? "guest" : userName;
				password = string.IsNullOrWhiteSpace(password) ? "guest" : password;
				virtualHost = string.IsNullOrWhiteSpace(virtualHost) ? "/" : virtualHost;

				if (string.IsNullOrWhiteSpace(hostName) ||
					port == 0 ||
					string.IsNullOrWhiteSpace(userName) ||
					string.IsNullOrWhiteSpace(password))
				{
					throw new InvalidOperationException("Некорректные настройки RabbitMQ! Проверьте конфигурацию.");
				}

				var factory = new ConnectionFactory
				{
					HostName = hostName,
					Port = port,
					UserName = userName,
					Password = password,
					VirtualHost = virtualHost
				};

				Log.Information("RabbitMQ ConnectionFactory created: amqp://{User}:***@{Host}:{Port}{VHost}",
					userName, hostName, port, virtualHost);

				return factory;
			});

			// Остальные сервисы
			services.AddSingleton<IRabbitMqBusService, RabbitMqBusService>();
			services.AddSingleton<IRabbitMqQueueListener<RabbitMqQueueListener>, RabbitMqQueueListener>();

			return services;
		}
	}
}
