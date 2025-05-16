using System.Text;
using lazy_light_requests_gate.headers;
using lazy_light_requests_gate.processing;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.Controllers;

[ApiController]
[Route("api/httpprotocol")]
public class HttpProtocolController : ControllerBase
{
	private readonly ILogger<HttpProtocolController> _logger;
	private readonly IHeaderValidationService _headerValidationService;
	private readonly IMessageProcessingService _messageProcessingService;
	private readonly IConfiguration _configuration;

	public HttpProtocolController(
		ILogger<HttpProtocolController> logger,
		IHeaderValidationService headerValidationService,
		IMessageProcessingService messageProcessingService,
		IConfiguration configuration)
	{
		_logger = logger;
		_headerValidationService = headerValidationService;
		_messageProcessingService = messageProcessingService;ы
		_configuration = configuration;
	}

	[HttpPost("push")]
	public async Task<IActionResult> PushMessage()
	{
		var companyName = _configuration["CompanyName"] ?? "default-company";
		var host = _configuration["Host"] ?? "localhost";
		var port = _configuration["Port"] ?? "5000";
		var validate = bool.TryParse(_configuration["Validate"], out var v) && v;
		var protocol = Request.Scheme;
		// 🔍 Логируем заголовки

		_logger.LogInformation("🔍 Заголовки запроса:");
		foreach (var header in Request.Headers)
		{
			_logger.LogInformation("{Key}: {Value}", header.Key, header.Value.ToString());
		}
		_logger.LogInformation("Content-Type: {ContentType}", Request.ContentType);

		// 🔍 Логируем тело запроса (один раз можно прочитать Body)
		Request.EnableBuffering(); // <- важно! позволяет читать тело повторно
		using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
		{
			var body = await reader.ReadToEndAsync();
			Request.Body.Position = 0; // сбрасываем позицию после чтения

			_logger.LogInformation("📦 Тело запроса: {Body}", body);
		}
		_logger.LogInformation("🔧 Параметры шлюза: Company={Company}, Host={Host}, Port={Port}, Validate={Validate}, Protocol={Protocol}",
			companyName, host, port, validate, protocol);

		var queueOut = $"{companyName.Trim().ToLower()}_out";
		var queueIn = $"{companyName.Trim().ToLower()}_in";

		var message = await new StreamReader(Request.Body).ReadToEndAsync();

		// SSE/HTTP Streaming Headers (опционально, если клиент слушает ответ как стрим)
		Response.Headers.Append("Content-Type", "text/event-stream");
		Response.Headers.Append("Cache-Control", "no-cache");
		Response.Headers.Append("Connection", "keep-alive");
		Response.Headers.Append("Access-Control-Allow-Origin", "*");

		if (validate)
		{
			var isValid = await _headerValidationService.ValidateHeadersAsync(Request.Headers);
			if (!isValid)
			{
				_logger.LogWarning("⚠️ Заголовки не прошли валидацию.");
				return BadRequest("Заголовки не прошли валидацию.");
			}
		}
		else
		{
			_logger.LogInformation("✅ Валидация отключена.");
		}

		LogHeaders();

		await _messageProcessingService.ProcessIncomingMessageAsync(
			message,
			queueOut,
			queueIn,
			host,
			int.Parse(port),
			protocol
		);

		return Ok("✅ Модель отправлена в шину и сохранена в БД.");
	}

	private void LogHeaders()
	{
		_logger.LogInformation("📋 Получены заголовки запроса:");
		foreach (var header in Request.Headers)
		{
			_logger.LogInformation("  {Header}: {Value}", header.Key, header.Value);
		}
	}
}
