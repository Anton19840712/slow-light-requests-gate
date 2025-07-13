using infrastructure.networking;
using lazy_light_requests_gate.temp.apptypeswitcher;

namespace infrastructure.services.background
{
	//1
	// Обновленный NetworkServerHostedService
	public class NetworkServerHostedService : BackgroundService
	{
		private readonly ILogger<NetworkServerHostedService> _logger;
		private readonly NetworkServerManager _serverManager;
		private readonly NetworkClientManager _clientManager;
		private readonly IConfiguration _configuration;
		private readonly IApplicationTypeService _applicationTypeService;

		public NetworkServerHostedService(
			ILogger<NetworkServerHostedService> logger,
			NetworkServerManager serverManager,
			NetworkClientManager clientManager,
			IConfiguration configuration,
			IApplicationTypeService applicationTypeService)
		{
			_logger = logger;
			_serverManager = serverManager;
			_clientManager = clientManager;
			_configuration = configuration;
			_applicationTypeService = applicationTypeService;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			//Проверяем тип приложения
			if (!_applicationTypeService.IsStreamEnabled())
			{
				_logger.LogInformation("Stream сервисы отключены. Тип приложения: {ApplicationType}",
					_applicationTypeService.GetDescription());
				return;
			}

			var protocol = _configuration["Protocol"]?.ToLowerInvariant();
			var mode = _configuration["Mode"]?.ToLowerInvariant();

			if (string.IsNullOrEmpty(protocol) || string.IsNullOrEmpty(mode))
			{
				_logger.LogWarning("Протокол или режим запуска не указан.");
				return;
			}

			_logger.LogInformation("Запуск Stream сервисов. Протокол: {Protocol}, Режим: {Mode}", protocol, mode);

			switch (mode)
			{
				case "server":
					_logger.LogInformation("Автозапуск сервера: {Protocol}", protocol);
					await _serverManager.StartServerAsync(protocol, stoppingToken);
					break;

				case "client":
					_logger.LogInformation("Автозапуск клиента: {Protocol}", protocol);
					await _clientManager.StartClientAsync(protocol, stoppingToken);
					break;

				default:
					_logger.LogWarning("Неизвестный режим запуска: {Mode}", mode);
					break;
			}
		}
	}
}
