using System.Text;
using lazy_light_requests_gate.core.application.interfaces.headers;
using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.presentation.attributes;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.controllers;

[ApiController]
[Route("api/httpprotocol")]
public class HttpProtocolController : ControllerBase
{
	private readonly ILogger<HttpProtocolController> _logger;
	private readonly IHeaderValidationService _headerValidationService;
	private readonly IMessageProcessingServiceFactory _messageProcessingServiceFactory;
	private readonly IConfiguration _configuration;
	private string _message;

	public HttpProtocolController(
		ILogger<HttpProtocolController> logger,
		IHeaderValidationService headerValidationService,
		IMessageProcessingServiceFactory messageProcessingServiceFactory,
		IConfiguration configuration)
	{
		_logger = logger;
		_headerValidationService = headerValidationService;
		_messageProcessingServiceFactory = messageProcessingServiceFactory;
		_configuration = configuration;
	}

	/// <summary>
	/// Основной endpoint для обработки HTTP сообщений
	/// Работает в режимах: RestOnly и Both
	/// </summary>
	[HttpPost("push")]
	[RequireRestRuntime] // ✅ Теперь правильно работает с Both режимом
	public async Task<IActionResult> PushMessage()
	{
		try
		{
			_logger.LogInformation("=== HTTP Protocol Push Request Started ===");

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
					return BadRequest(new
					{
						error = "Header validation failed",
						message = "Заголовки не прошли валидацию",
						timestamp = DateTime.UtcNow
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

			_logger.LogInformation("Message processed successfully, size: {Size} characters", _message?.Length ?? 0);

			return Ok(new
			{
				message = "Модель принята и обработана",
				processed = true,
				timestamp = DateTime.UtcNow,
				messageSize = _message?.Length ?? 0,
				database = _messageProcessingServiceFactory.GetCurrentDatabaseType(),
				company = config.CompanyName
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing HTTP push message");
			return StatusCode(500, new
			{
				error = "Internal server error",
				message = "Ошибка при обработке сообщения",
				details = ex.Message,
				timestamp = DateTime.UtcNow
			});
		}
	}

	/// <summary>
	/// Получить статус HTTP Protocol сервиса
	/// </summary>
	[HttpGet("status")]
	[RequireEitherAPIRuntime]
	public IActionResult GetStatus()
	{
		try
		{
			var config = GetRequestConfiguration();

			return Ok(new
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
				},
				timestamp = DateTime.UtcNow
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting HTTP Protocol status");
			return StatusCode(500, new
			{
				error = "Failed to get status",
				details = ex.Message,
				timestamp = DateTime.UtcNow
			});
		}
	}

	/// <summary>
	/// Health check endpoint
	/// </summary>
	[HttpGet("health")]
	[RequireEitherAPIRuntime]
	public IActionResult HealthCheck()
	{
		return Ok(new
		{
			service = "HttpProtocol",
			health = "healthy",
			database = _messageProcessingServiceFactory.GetCurrentDatabaseType(),
			timestamp = DateTime.UtcNow
		});
	}

	#region Private Methods

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
			// Скрываем чувствительные заголовки
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
		Request.EnableBuffering(); // Важно! Позволяет читать тело повторно

		using var reader = new StreamReader(Request.Body, Encoding.UTF8,
			detectEncodingFromByteOrderMarks: false, leaveOpen: true);

		_message = await reader.ReadToEndAsync();
		Request.Body.Position = 0; // Сбрасываем позицию для повторного чтения

		_logger.LogInformation("Request body received, size: {Size} characters", _message?.Length ?? 0);

		// Логируем тело только если оно не слишком большое
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