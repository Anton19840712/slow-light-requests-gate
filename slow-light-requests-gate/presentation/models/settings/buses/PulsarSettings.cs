using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.presentation.models.settings.buses
{
	/// <summary>
	/// Настройки для Apache Pulsar
	/// </summary>
	public class PulsarSettings : MessageBusBaseSettings
	{
		/// <summary>
		/// URL сервиса Pulsar (например: pulsar://localhost:6650)
		/// </summary>
		[Required]
		public string ServiceUrl { get; set; } = "pulsar://localhost:6650";

		/// <summary>
		/// Tenant в Pulsar (по умолчанию: public)
		/// </summary>
		public string Tenant { get; set; } = "public";

		/// <summary>
		/// Namespace в Pulsar (по умолчанию: default)
		/// </summary>
		public string Namespace { get; set; } = "default";

		/// <summary>
		/// Топик для входящих сообщений
		/// </summary>
		[Required]
		public string InputTopic { get; set; } = "gateway-input";

		/// <summary>
		/// Топик для исходящих сообщений
		/// </summary>
		[Required]
		public string OutputTopic { get; set; } = "gateway-output";

		/// <summary>
		/// Имя подписки для consumer
		/// </summary>
		[Required]
		public string SubscriptionName { get; set; } = "gateway-subscription";

		/// <summary>
		/// Тип подписки (по умолчанию: Exclusive)
		/// </summary>
		public string SubscriptionType { get; set; } = "Exclusive";

		/// <summary>
		/// Таймаут соединения в секундах
		/// </summary>
		[Range(1, 300)]
		public int ConnectionTimeoutSeconds { get; set; } = 15;

		/// <summary>
		/// Максимальное количество попыток переподключения
		/// </summary>
		[Range(0, 10)]
		public int MaxReconnectAttempts { get; set; } = 3;

		/// <summary>
		/// Интервал между попытками переподключения в секундах
		/// </summary>
		[Range(1, 60)]
		public int ReconnectIntervalSeconds { get; set; } = 5;

		/// <summary>
		/// Включить компрессию сообщений
		/// </summary>
		public bool EnableCompression { get; set; } = false;

		/// <summary>
		/// Тип компрессии (если включена)
		/// </summary>
		public string CompressionType { get; set; } = "LZ4";

		/// <summary>
		/// Размер batch для producer
		/// </summary>
		[Range(1, 10000)]
		public int BatchSize { get; set; } = 1000;

		/// <summary>
		/// Максимальное время ожидания для batch в миллисекундах
		/// </summary>
		[Range(1, 60000)]
		public int BatchingMaxPublishDelayMs { get; set; } = 10;

		/// <summary>
		/// Получить полное имя топика для входящих сообщений
		/// </summary>
		/// <returns>Полное имя топика в формате persistent://tenant/namespace/topic</returns>
		public string GetFullInputTopicName()
		{
			return $"persistent://{Tenant}/{Namespace}/{InputTopic}";
		}

		/// <summary>
		/// Получить полное имя топика для исходящих сообщений
		/// </summary>
		/// <returns>Полное имя топика в формате persistent://tenant/namespace/topic</returns>
		public string GetFullOutputTopicName()
		{
			return $"persistent://{Tenant}/{Namespace}/{OutputTopic}";
		}

		/// <summary>
		/// Получить полное имя произвольного топика
		/// </summary>
		/// <param name="topicName">Имя топика</param>
		/// <returns>Полное имя топика в формате persistent://tenant/namespace/topic</returns>
		public string GetFullTopicName(string topicName)
		{
			if (string.IsNullOrWhiteSpace(topicName))
				throw new ArgumentException("Topic name cannot be null or empty", nameof(topicName));

			return $"persistent://{Tenant}/{Namespace}/{topicName}";
		}

		/// <summary>
		/// Валидация настроек Pulsar
		/// </summary>
		/// <returns>Результат валидации</returns>
		public (bool IsValid, string ErrorMessage) Validate()
		{
			if (string.IsNullOrWhiteSpace(ServiceUrl))
				return (false, "ServiceUrl is required");

			if (!Uri.TryCreate(ServiceUrl, UriKind.Absolute, out var uri))
				return (false, "ServiceUrl must be a valid URI");

			if (uri.Scheme != "pulsar" && uri.Scheme != "pulsar+ssl")
				return (false, "ServiceUrl must use 'pulsar://' or 'pulsar+ssl://' scheme");

			if (string.IsNullOrWhiteSpace(Tenant))
				return (false, "Tenant is required");

			if (string.IsNullOrWhiteSpace(Namespace))
				return (false, "Namespace is required");

			if (string.IsNullOrWhiteSpace(InputTopic))
				return (false, "InputTopic is required");

			if (string.IsNullOrWhiteSpace(OutputTopic))
				return (false, "OutputTopic is required");

			if (string.IsNullOrWhiteSpace(SubscriptionName))
				return (false, "SubscriptionName is required");

			// Проверка допустимых типов подписки
			var validSubscriptionTypes = new[] { "Exclusive", "Shared", "Failover", "KeyShared" };
			if (!validSubscriptionTypes.Contains(SubscriptionType))
				return (false, $"SubscriptionType must be one of: {string.Join(", ", validSubscriptionTypes)}");

			// Проверка типов компрессии
			if (EnableCompression)
			{
				var validCompressionTypes = new[] { "NONE", "LZ4", "ZLIB", "ZSTD", "SNAPPY" };
				if (!validCompressionTypes.Contains(CompressionType.ToUpper()))
					return (false, $"CompressionType must be one of: {string.Join(", ", validCompressionTypes)}");
			}

			return (true, string.Empty);
		}

		/// <summary>
		/// Получить строку подключения для логирования (без чувствительных данных)
		/// </summary>
		/// <returns>Безопасная строка для логирования</returns>
		public string GetConnectionStringForLogging()
		{
			var uri = new Uri(ServiceUrl);
			return $"pulsar://{uri.Host}:{uri.Port}/{Tenant}/{Namespace}";
		}
	}
}