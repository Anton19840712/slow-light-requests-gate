using System.Text;
using lazy_light_requests_gate.core.application.interfaces.headers;
using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.presentation.attributes;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.controllers;

[ApiController]
[Route("api/httpprotocol")]
public class HttpProtocolController : BaseGatewayController
{
	private readonly IHeaderValidationService _headerValidationService;
	private readonly IMessageProcessingServiceFactory _messageProcessingServiceFactory;
	private readonly IConfiguration _configuration;
	private string _message;

	public HttpProtocolController(
		ILogger<HttpProtocolController> logger,
		IHeaderValidationService headerValidationService,
		IMessageProcessingServiceFactory messageProcessingServiceFactory,
		IConfiguration configuration) : base(logger)
	{
		_headerValidationService = headerValidationService;
		_messageProcessingServiceFactory = messageProcessingServiceFactory;
		_configuration = configuration;
	}

	/// <summary>
	/// Основной endpoint для обработки HTTP сообщений
	/// Работает в режимах: RestOnly и Both
	/// </summary>
	[HttpPost("push")]
	[RequireRestRuntime]
	public async Task<IActionResult> PushMessage()
	{
		return await SafeExecuteAsync(async () =>
		{
			LogOperationStart("PushMessage");

			// Получаем конфигурацию
			var config = GetRequestConfiguration();
			_logger.LogInformation("Configuration loaded: Company={Company}, Host={Host}, Protocol={Protocol}",
				config.CompanyName, config.Host, config.Protocol);

			// Читаем и логируем заголовки
			LogRequestHeaders();

			// Читаем тело запроса
			await ReadRequestBodyAsync();

			// Валидация заголовков (если включена)
			if (config.Validate)
			{
				_logger.LogInformation("Validating headers...");
				var isValid = await _headerValidationService.ValidateHeadersAsync(Request.Headers);
				if (!isValid)
				{
					_logger.LogWarning("Header validation failed");
					return ErrorResponse("Header validation failed", 400, new
					{
						message = "Заголовки не прошли валидацию",
						validationEnabled = config.Validate
					});
				}
				_logger.LogInformation("Header validation passed");
			}
			else
			{
				_logger.LogInformation("Header validation is disabled");
			}

			// Обрабатываем сообщение
			await ProcessMessageAsync(config);

			var result = new
			{
				message = "Модель принята и обработана",
				processed = true,
				messageSize = _message?.Length ?? 0,
				database = _messageProcessingServiceFactory.GetCurrentDatabaseType(),
				company = config.CompanyName,
				configuration = new
				{
					validation = config.Validate ? "enabled" : "disabled",
					protocol = config.Protocol,
					host = config.Host
				}
			};

			LogOperationEnd("PushMessage", result);
			return SuccessResponse(result, "Сообщение успешно обработано");

		}, "PushMessage", "HTTP сообщение обработано успешно");
	}

	/// <summary>
	/// Получить статус HTTP Protocol сервиса
	/// </summary>
	[HttpGet("status")]
	[RequireEitherAPIRuntime]
	public IActionResult GetStatus()
	{
		return SafeExecute(() =>
		{
			LogOperationStart("GetStatus");

			var config = GetRequestConfiguration();

			var statusData = new
			{
				service = "HttpProtocol",
				status = "active",
				validation = config.Validate ? "enabled" : "disabled",
				currentDatabase = _messageProcessingServiceFactory.GetCurrentDatabaseType(),
				configuration = new
				{
					host = config.Host,
					protocol = config.Protocol,
					company = config.CompanyName,
					inputChannel = config.QueueIn,
					outputChannel = config.QueueOut
				},
				endpoints = new[]
				{
					"POST /api/httpprotocol/push",
					"GET /api/httpprotocol/status",
					"GET /api/httpprotocol/health"
				}
			};

			LogOperationEnd("GetStatus", statusData);
			return SuccessResponse(statusData, "HTTP Protocol статус получен");

		}, "GetStatus");
	}

	/// <summary>
	/// Health check endpoint
	/// </summary>
	[HttpGet("health")]
	[RequireEitherAPIRuntime]
	public IActionResult HealthCheck()
	{
		return SafeExecute(() =>
		{
			var healthData = new
			{
				service = "HttpProtocol",
				health = "healthy",
				database = _messageProcessingServiceFactory.GetCurrentDatabaseType(),
				uptime = GetServiceUptime()
			};

			return SuccessResponse(healthData, "Сервис работает нормально");

		}, "HealthCheck");
	}

	/// <summary>
	/// Получить метрики производительности
	/// </summary>
	[HttpGet("metrics")]
	[RequireEitherAPIRuntime]
	public IActionResult GetMetrics()
	{
		return SafeExecute(() =>
		{
			var metrics = new
			{
				service = "HttpProtocol",
				requestsProcessed = GetProcessedRequestsCount(),
				averageProcessingTime = GetAverageProcessingTime(),
				lastProcessedMessage = GetLastProcessedMessageInfo(),
				currentDatabase = _messageProcessingServiceFactory.GetCurrentDatabaseType()
			};

			return SuccessResponse(metrics, "Метрики получены");

		}, "GetMetrics");
	}

	#region Private Methods

	private IActionResult SafeExecute(Func<IActionResult> operation, string operationName)
	{
		try
		{
			return operation();
		}
		catch (Exception ex)
		{
			return HandleException(ex, operationName);
		}
	}

	private RequestConfiguration GetRequestConfiguration()
	{
		return new RequestConfiguration
		{
			CompanyName = _configuration["CompanyName"] ?? "default-company",
			Host = _configuration["Host"] ?? "localhost",
			PortHttp = _configuration["PortHttp"] ?? "80",
			PortHttps = _configuration["PortHttps"] ?? "443",
			Validate = bool.TryParse(_configuration["Validate"], out var v) && v,
			Protocol = Request.Scheme,
			QueueIn = _configuration["InputChannel"],
			QueueOut = _configuration["OutputChannel"]
		};
	}

	private void LogRequestHeaders()
	{
		_logger.LogInformation("=== Request Headers ===");
		_logger.LogInformation("Content-Type: {ContentType}", Request.ContentType ?? "not specified");

		foreach (var header in Request.Headers)
		{
			if (IsSensitiveHeader(header.Key))
			{
				_logger.LogInformation("{Header}: [HIDDEN]", header.Key);
			}
			else
			{
				_logger.LogInformation("{Header}: {Value}", header.Key, header.Value.ToString());
			}
		}
	}

	private async Task ReadRequestBodyAsync()
	{
		Request.EnableBuffering();

		using var reader = new StreamReader(Request.Body, Encoding.UTF8,
			detectEncodingFromByteOrderMarks: false, leaveOpen: true);

		_message = await reader.ReadToEndAsync();
		Request.Body.Position = 0;

		_logger.LogInformation("Request body received, size: {Size} characters", _message?.Length ?? 0);

		if (_message != null && _message.Length <= 2000)
		{
			_logger.LogInformation("Request body: {Body}", _message);
		}
		else
		{
			_logger.LogInformation("Request body too large for logging (size: {Size})", _message?.Length ?? 0);
		}
	}

	private async Task ProcessMessageAsync(RequestConfiguration config)
	{
		var currentDatabaseType = _messageProcessingServiceFactory.GetCurrentDatabaseType();
		_logger.LogInformation("Processing message using database: {DatabaseType}", currentDatabaseType);

		var messageProcessingService = _messageProcessingServiceFactory.CreateMessageProcessingService(currentDatabaseType);

		await messageProcessingService.ProcessForSaveIncomingMessageAsync(
			_message,
			config.QueueOut,
			config.QueueIn,
			config.Host,
			int.TryParse(config.PortHttp, out var portInt) ? portInt : null,
			config.Protocol
		);

		_logger.LogInformation("Message successfully processed and saved to {DatabaseType}", currentDatabaseType);
	}

	private static bool IsSensitiveHeader(string headerName)
	{
		var sensitiveHeaders = new[]
		{
			"authorization",
			"cookie",
			"x-api-key",
			"x-auth-token",
			"x-password",
			"x-secret"
		};

		return sensitiveHeaders.Contains(headerName.ToLowerInvariant());
	}

	// Заглушки для метрик - реализуйте согласно вашей архитектуре
	private TimeSpan GetServiceUptime()
	{
		// Реализуйте логику получения времени работы сервиса
		return TimeSpan.FromMinutes(1);
	}

	private int GetProcessedRequestsCount()
	{
		// Реализуйте логику подсчета обработанных запросов
		return 0;
	}

	private double GetAverageProcessingTime()
	{
		// Реализуйте логику подсчета среднего времени обработки
		return 0.0;
	}

	private object GetLastProcessedMessageInfo()
	{
		// Реализуйте логику получения информации о последнем обработанном сообщении
		return new { timestamp = DateTime.UtcNow, size = _message?.Length ?? 0 };
	}

	#endregion

	#region Helper Classes

	private class RequestConfiguration
	{
		public string CompanyName { get; set; }
		public string Host { get; set; }
		public string PortHttp { get; set; }
		public string PortHttps { get; set; }
		public bool Validate { get; set; }
		public string Protocol { get; set; }
		public string QueueIn { get; set; }
		public string QueueOut { get; set; }
	}

	#endregion
}
