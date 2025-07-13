using lazy_light_requests_gate.core.application.interfaces.runtime;
using lazy_light_requests_gate.core.domain.events;
using lazy_light_requests_gate.presentation.enums;

namespace lazy_light_requests_gate.infrastructure.services.runtime
{
	public class ApplicationTypeService : IApplicationTypeService
	{
		private volatile ApplicationType _currentType;
		private readonly ILogger<ApplicationTypeService> _logger;
		private readonly IConfiguration _configuration;

		public event EventHandler<ApplicationTypeChangedEventArgs> ApplicationTypeChanged;

		public ApplicationTypeService(ILogger<ApplicationTypeService> logger, IConfiguration configuration)
		{
			_logger = logger;
			_configuration = configuration;
			_currentType = GetInitialType();
		}

		public ApplicationType GetApplicationType() => _currentType;

		public async Task SetApplicationTypeAsync(ApplicationType type)
		{
			var oldType = _currentType;
			_currentType = type;

			_logger.LogInformation("Application type changed from {OldType} to {NewType}", oldType, type);

			// Сохраняем в конфигурацию для сохранения между перезапусками
			await SaveConfigurationAsync(type);

			// Уведомляем подписчиков об изменении
			ApplicationTypeChanged?.Invoke(this, new ApplicationTypeChangedEventArgs(oldType, type));
		}
		public bool IsRestEnabled() => _currentType == ApplicationType.RestOnly || _currentType == ApplicationType.Both;
		public bool IsStreamEnabled() => _currentType == ApplicationType.StreamOnly || _currentType == ApplicationType.Both;
		public bool IsBothEnabled() => _currentType == ApplicationType.Both;

		public string GetDescription() => _currentType switch
		{
			ApplicationType.RestOnly => "Только REST API",
			ApplicationType.StreamOnly => "Только Stream протоколы",
			ApplicationType.Both => "REST API и Stream протоколы",
			_ => "Неизвестный тип"
		};

		private ApplicationType GetInitialType()
		{
			var configValue = _configuration["ApplicationType"];

			// Нормализуем значение из конфига к enum значениям
			var normalizedValue = configValue?.ToLowerInvariant() switch
			{
				"restonly" => "RestOnly",
				"streamonly" => "StreamOnly",
				"both" => "Both",
				_ => "Both" // по умолчанию
			};

			return Enum.TryParse<ApplicationType>(normalizedValue, out var type) ? type : ApplicationType.Both;
		}

		private async Task SaveConfigurationAsync(ApplicationType type)
		{
			_logger.LogInformation("Saving configuration: ApplicationType = {Type}", type);
			// Здесь можно добавить сохранение в файл или базу данных
			await Task.CompletedTask;
		}
	}
}
