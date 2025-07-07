using application.interfaces.messaging;
using domain.models.configurationsettings;
using domain.models.response;
using System.Collections.Concurrent;
using System.Text.Json;

namespace infrastructure.messaging
{
	public class UnifiedMessageBusManager
	{
		private readonly ILogger<UnifiedMessageBusManager> _logger;
		private readonly IMessageBusFactory _messageBusFactory;
		private readonly IMessageBusConfigurationProvider _configProvider;
		private readonly ConcurrentDictionary<string, (IMessageBusService Service, MessageBusBaseSettings Config)> _buses = new();

		public UnifiedMessageBusManager(
			IServiceProvider serviceProvider,
			ILogger<UnifiedMessageBusManager> logger,
			IMessageBusFactory messageBusFactory,
			IMessageBusConfigurationProvider configProvider)
		{
			_logger = logger;
			_messageBusFactory = messageBusFactory;
			_configProvider = configProvider;
		}

		// Запуск из файла конфигурации (для BackgroundService)
		public async Task StartFromConfigFileAsync(CancellationToken cancellationToken)
		{
			var busTypeConfig = _configProvider.GetConfiguration();
			await StartBusAsync(busTypeConfig, cancellationToken);
		}

		// Запуск из JSON (для API контроллера)
		public async Task StartFromJsonAsync(JsonDocument json, CancellationToken cancellationToken)
		{

			if (!json.RootElement.TryGetProperty("gateWayType", out var gatewayTypeProp))
				throw new InvalidOperationException("Поле 'gateWayType' не найдено");

			var gatewayType = gatewayTypeProp.GetString();
			var config = _configProvider.GetConfiguration(json);

			await StartBusAsync(config, cancellationToken);
		}

		// Единый метод запуска
		public async Task StartBusAsync(MessageBusBaseSettings config, CancellationToken cancellationToken)
		{
			var id = config.InstanceNetworkGateId;

			if (_buses.ContainsKey(id))
			{
				// ты перезапустишь шину, если передашь точно такой же id:
				_logger.LogInformation("Остановка уже запущенной шины {Id}", id);
				await StopBusAsync(id, cancellationToken);
			}

			// создаешь транспорт согласно конфигурации
			var bus = _messageBusFactory.Create(config);
			await bus.StartAsync(config, cancellationToken);

			if (!_buses.TryAdd(id, (bus, config)))
			{
				_logger.LogWarning("Не удалось добавить шину {Id} в пул", id);
			}
			else
			{
				_logger.LogInformation("UnifiedMessageBusManager: instance шины {Id} ({Type}) успешно запущен.", id, config.TypeToRun);
			}
		}

		public async Task StopBusAsync(string id, CancellationToken cancellationToken)
		{
			try
			{
				if (_buses.TryRemove(id, out var busInfo))
				{
					await busInfo.Service.StopAsync(cancellationToken);
					_logger.LogInformation("RabbitMqService: Остановка подключения с id {Id}", id);
					return;
				}

				_logger.LogWarning("Шина с id {Id} не найдена", id);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Ошибка при остановке шины с id {Id}", id);
			}
		}

		public IEnumerable<string> GetRunningBusIds() => _buses.Keys;

		public IEnumerable<BusInformationDto> GetRunningBusInfo()
		{
			return _buses.Select(amq => new BusInformationDto
			{
				InstanceId = amq.Value.Config.InstanceNetworkGateId,
				TypeToRun = amq.Value.Config.TypeToRun.ToString(),
			});
		}

		public async Task StopAllBusesAsync(CancellationToken cancellationToken)
		{
			var busIds = _buses.Keys.ToList();
			foreach (var id in busIds)
			{
				await StopBusAsync(id, cancellationToken);
			}
		}
	}
}
