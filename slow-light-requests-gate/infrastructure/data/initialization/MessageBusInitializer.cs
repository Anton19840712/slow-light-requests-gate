using lazy_light_requests_gate.core.application.interfaces.buses;

namespace lazy_light_requests_gate.infrastructure.data.initialization
{
	public class MessageBusInitializer
	{
		public async Task InitializeMessageBusAsync(IServiceProvider services, IConfiguration configuration, string selectedBus)
		{
			var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

			switch (selectedBus)
			{
				case "rabbit":
					await InitializeRabbitMqAsync(services, configuration, timestamp);
					break;
				case "activemq":
					await InitializeActiveMqAsync(services, configuration, timestamp);
					break;
				case "kafka":
					await InitializeKafkaAsync(services, configuration, timestamp);
					break;
				case "pulsar":
					await InitializePulsarAsync(services, configuration, timestamp);
					break;
				case "tarantool":
					await InitializeTarantoolAsync(services, configuration, timestamp);
					break;
				default:
					throw new InvalidOperationException($"Неподдерживаемая шина сообщений: {selectedBus}");
			}
		}

		private async Task InitializeRabbitMqAsync(IServiceProvider services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [INIT] Инициализация ТОЛЬКО RabbitMQ...");

			try
			{
				var rabbitService = services.GetRequiredService<IRabbitMqBusService>();
				await rabbitService.TestConnectionAsync();
				Console.WriteLine($"[{timestamp}] [SUCCESS] RabbitMQ подключение протестировано успешно");

				var queueName = configuration["RabbitMqSettings:ListenQueueName"];
				if (!string.IsNullOrEmpty(queueName))
				{
					await rabbitService.StartListeningAsync(queueName, CancellationToken.None);
					Console.WriteLine($"[{timestamp}] [SUCCESS] RabbitMQ слушатель запущен для очереди: {queueName}");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{timestamp}] [ERROR] Ошибка инициализации RabbitMQ: {ex.Message}");
				throw;
			}
		}

		private async Task InitializeActiveMqAsync(IServiceProvider services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [INIT] Инициализация ТОЛЬКО ActiveMQ...");

			try
			{
				var activeMqService = services.GetRequiredService<IActiveMqService>();
				Console.WriteLine($"[{timestamp}] [INIT] ActiveMQ сервис получен");

				var connectionTest = await activeMqService.TestConnectionAsync();
				if (connectionTest)
				{
					Console.WriteLine($"[{timestamp}] [SUCCESS] ActiveMQ подключение протестировано успешно");

					var queueName = configuration["ActiveMqSettings:ListenQueueName"];
					if (!string.IsNullOrEmpty(queueName))
					{
						await activeMqService.StartListeningAsync(queueName, CancellationToken.None);
						Console.WriteLine($"[{timestamp}] [SUCCESS] ActiveMQ слушатель запущен для очереди: {queueName}");
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{timestamp}] [ERROR] Ошибка инициализации ActiveMQ: {ex.Message}");
				throw;
			}
		}

		private async Task InitializeKafkaAsync(IServiceProvider services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [INIT] Инициализация ТОЛЬКО Kafka...");

			try
			{
				var kafkaService = services.GetService<IKafkaStreamsService>();
				if (kafkaService != null)
				{
					Console.WriteLine($"[{timestamp}] [INIT] Kafka сервис получен");

					var connectionTest = await kafkaService.TestConnectionAsync();
					if (connectionTest)
					{
						Console.WriteLine($"[{timestamp}] [SUCCESS] Kafka подключение протестировано успешно");

						await kafkaService.StartAsync(CancellationToken.None);
						Console.WriteLine($"[{timestamp}] [SUCCESS] Kafka Streams запущен");

						var topicName = configuration["KafkaStreamsSettings:InputTopic"];
						if (!string.IsNullOrEmpty(topicName))
						{
							await kafkaService.StartListeningAsync(topicName, CancellationToken.None);
							Console.WriteLine($"[{timestamp}] [SUCCESS] Kafka слушатель запущен для топика: {topicName}");
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{timestamp}] [ERROR] Ошибка инициализации Kafka: {ex.Message}");
				throw;
			}
		}

		private async Task InitializePulsarAsync(IServiceProvider services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [INIT] Инициализация ТОЛЬКО Pulsar...");

			try
			{
				var pulsarService = services.GetService<IPulsarService>();
				if (pulsarService != null)
				{
					Console.WriteLine($"[{timestamp}] [INIT] Pulsar сервис получен");

					var connectionTest = await pulsarService.TestConnectionAsync();
					if (connectionTest)
					{
						Console.WriteLine($"[{timestamp}] [SUCCESS] Pulsar подключение протестировано успешно");

						var topicName = configuration["PulsarSettings:InputTopic"];
						if (!string.IsNullOrEmpty(topicName))
						{
							await pulsarService.StartListeningAsync(topicName, CancellationToken.None);
							Console.WriteLine($"[{timestamp}] [SUCCESS] Pulsar слушатель запущен для топика: {topicName}");
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{timestamp}] [ERROR] Ошибка инициализации Pulsar: {ex.Message}");
				throw;
			}
		}

		private async Task InitializeTarantoolAsync(IServiceProvider services, IConfiguration configuration, string timestamp)
		{
			Console.WriteLine($"[{timestamp}] [INIT] Инициализация ТОЛЬКО Tarantool...");

			try
			{
				var tarantoolService = services.GetService<ITarantoolService>();
				if (tarantoolService != null)
				{
					Console.WriteLine($"[{timestamp}] [INIT] Tarantool сервис получен");

					var connectionTest = await tarantoolService.TestConnectionAsync();
					if (connectionTest)
					{
						Console.WriteLine($"[{timestamp}] [SUCCESS] Tarantool подключение протестировано успешно");

						var spaceName = configuration["TarantoolSettings:OutputSpace"];
						if (!string.IsNullOrEmpty(spaceName))
						{
							await tarantoolService.StartListeningAsync(spaceName, CancellationToken.None);
							Console.WriteLine($"[{timestamp}] [SUCCESS] Tarantool слушатель запущен для пространства: {spaceName}");
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{timestamp}] [ERROR] Ошибка инициализации Tarantool: {ex.Message}");
				throw;
			}
		}
	}
}
