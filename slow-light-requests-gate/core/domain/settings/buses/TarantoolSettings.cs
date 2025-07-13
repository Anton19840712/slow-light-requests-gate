namespace lazy_light_requests_gate.core.domain.settings.buses
{
	using System.ComponentModel.DataAnnotations;

	namespace lazy_light_requests_gate.core.domain.settings.buses
	{
		/// <summary>
		/// Настройки для подключения к Tarantool
		/// </summary>
		public class TarantoolSettings : MessageBusBaseSettings
		{

			/// <summary>
			/// Хост Tarantool сервера
			/// </summary>
			[Required]
			public string Host { get; set; } = "localhost";

			/// <summary>
			/// Порт Tarantool сервера
			/// </summary>
			[Range(1, 65535, ErrorMessage = "Порт должен быть от 1 до 65535")]
			public int Port { get; set; } = 3301;

			/// <summary>
			/// Имя пользователя для аутентификации (необязательно)
			/// </summary>
			public string Username { get; set; } = "";

			/// <summary>
			/// Пароль для аутентификации (необязательно)
			/// </summary>
			public string Password { get; set; } = "";

			/// <summary>
			/// Название потока для группировки сообщений
			/// </summary>
			[Required]
			public string StreamName { get; set; } = "default-stream";

			/// <summary>
			/// Таймаут подключения в секундах
			/// </summary>
			[Range(1, 300, ErrorMessage = "Таймаут подключения должен быть от 1 до 300 секунд")]
			public int ConnectionTimeoutSeconds { get; set; } = 30;

			/// <summary>
			/// Максимальное количество попыток переподключения
			/// </summary>
			[Range(0, 10, ErrorMessage = "Количество попыток переподключения должно быть от 0 до 10")]
			public int MaxReconnectAttempts { get; set; } = 3;

			/// <summary>
			/// Интервал между попытками переподключения в секундах
			/// </summary>
			[Range(1, 60, ErrorMessage = "Интервал переподключения должен быть от 1 до 60 секунд")]
			public int ReconnectIntervalSeconds { get; set; } = 5;

			/// <summary>
			/// Интервал опроса для получения новых сообщений в миллисекундах
			/// </summary>
			[Range(100, 10000, ErrorMessage = "Интервал опроса должен быть от 100 до 10000 мс")]
			public int PollingIntervalMs { get; set; } = 1000;

			/// <summary>
			/// Максимальное количество сообщений, получаемых за один запрос
			/// </summary>
			[Range(1, 1000, ErrorMessage = "Размер пакета должен быть от 1 до 1000 сообщений")]
			public int BatchSize { get; set; } = 100;

			/// <summary>
			/// Автоматически создавать spaces при подключении
			/// </summary>
			public bool AutoCreateSpaces { get; set; } = true;

			/// <summary>
			/// Автоматически удалять обработанные сообщения
			/// </summary>
			public bool AutoDeleteProcessedMessages { get; set; } = true;

			/// <summary>
			/// Включить сжатие данных
			/// </summary>
			public bool EnableCompression { get; set; } = false;

			/// <summary>
			/// Формат временных меток (ISO, Unix, Custom)
			/// </summary>
			public string TimestampFormat { get; set; } = "ISO";

			/// <summary>
			/// Строка подключения (альтернативный способ задания настроек)
			/// Формат: tarantool://username:password@host:port
			/// </summary>
			public string ConnectionString { get; set; } = "";

			/// <summary>
			/// Проверка валидности настроек
			/// </summary>
			/// <returns>Список ошибок валидации</returns>
			public List<string> Validate()
			{
				var errors = new List<string>();

				if (string.IsNullOrWhiteSpace(Host))
					errors.Add("Host не может быть пустым");

				if (Port <= 0 || Port > 65535)
					errors.Add("Port должен быть от 1 до 65535");

				if (string.IsNullOrWhiteSpace(InputChannel))
					errors.Add("InputSpace не может быть пустым");

				if (string.IsNullOrWhiteSpace(OutputChannel))
					errors.Add("OutputSpace не может быть пустым");

				if (string.IsNullOrWhiteSpace(StreamName))
					errors.Add("StreamName не может быть пустым");

				if (InputChannel == OutputChannel)
					errors.Add("InputSpace и OutputSpace должны различаться");

				// Проверка строки подключения, если она задана
				if (!string.IsNullOrWhiteSpace(ConnectionString))
				{
					try
					{
						var uri = new Uri(ConnectionString);
						if (uri.Scheme != "tarantool")
							errors.Add("ConnectionString должна начинаться с 'tarantool://'");
					}
					catch
					{
						errors.Add("Некорректный формат ConnectionString");
					}
				}

				return errors;
			}

			/// <summary>
			/// Получить строку подключения на основе настроек
			/// </summary>
			/// <returns>Строка подключения</returns>
			public string GetConnectionString()
			{
				if (!string.IsNullOrWhiteSpace(ConnectionString))
					return ConnectionString;

				if (!string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password))
					return $"tarantool://{Username}:{Password}@{Host}:{Port}";

				return $"{Host}:{Port}";
			}

			/// <summary>
			/// Проверка, нужна ли аутентификация
			/// </summary>
			/// <returns>True, если нужна аутентификация</returns>
			public bool RequiresAuthentication()
			{
				return !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
			}

			/// <summary>
			/// Получить отображаемое описание настроек (без чувствительных данных)
			/// </summary>
			/// <returns>Строка с описанием</returns>
			public override string ToString()
			{
				var auth = RequiresAuthentication() ? $" (Auth: {Username})" : " (Guest)";
				return $"Tarantool: {Host}:{Port}{auth}, Spaces: {InputChannel} -> {OutputChannel}, Stream: {StreamName}";
			}
		}
	}
}
