using lazy_light_requests_gate.infrastructure.networking;
using lazy_light_requests_gate.presentation.attributes;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.controllers
{
	/// <summary>
	/// Контроллер для управления серверными stream подключениями
	/// </summary>
	[ApiController]
	[Route("api/[controller]")]
	public class ServerStreamController : BaseGatewayController
	{
		private readonly NetworkServerManager _manager;

		public ServerStreamController(
			NetworkServerManager manager,
			ILogger<ServerStreamController> logger) : base(logger)
		{
			_manager = manager;
		}

		/// <summary>
		/// Запуск сервера для указанного протокола
		/// </summary>
		[HttpPost("start/{protocol}")]
		[RequireStreamRuntime]
		public async Task<IActionResult> Start(string protocol)
		{
			return await SafeExecuteAsync(async () =>
			{
				LogOperationStart("StartServer", new { protocol });

				// Валидация протокола
				var validation = ValidateRequest(
					(!string.IsNullOrWhiteSpace(protocol), "Protocol name is required"),
					(IsValidProtocol(protocol), $"Unsupported protocol: {protocol}")
				);
				if (validation != null) return validation;

				await _manager.StartServerAsync(protocol, HttpContext.RequestAborted);

				var result = new
				{
					message = $"Сервер {protocol} запущен успешно",
					protocol = protocol,
					status = "started",
					startedAt = DateTime.UtcNow
				};

				LogOperationEnd("StartServer", result);
				return SuccessResponse(result, $"Сервер {protocol} запущен");

			}, "StartServer", $"Server {protocol} started successfully", new { protocol });
		}

		/// <summary>
		/// Остановка сервера для указанного протокола
		/// </summary>
		[HttpPost("stop/{protocol}")]
		[RequireStreamRuntime]
		public async Task<IActionResult> Stop(string protocol)
		{
			return await SafeExecuteAsync(async () =>
			{
				LogOperationStart("StopServer", new { protocol });

				// Валидация протокола
				var validation = ValidateRequest(
					(!string.IsNullOrWhiteSpace(protocol), "Protocol name is required")
				);
				if (validation != null) return validation;

				await _manager.StopServerAsync(protocol, HttpContext.RequestAborted);

				var result = new
				{
					message = $"Сервер {protocol} остановлен успешно",
					protocol = protocol,
					status = "stopped",
					stoppedAt = DateTime.UtcNow
				};

				LogOperationEnd("StopServer", result);
				return SuccessResponse(result, $"Сервер {protocol} остановлен");

			}, "StopServer", $"Server {protocol} stopped successfully", new { protocol });
		}

		/// <summary>
		/// Получение статуса всех серверных подключений
		/// </summary>
		[HttpGet("status")]
		[RequireStreamRuntime]
		public IActionResult Status()
		{
			return SafeExecute(() =>
			{
				LogOperationStart("GetServersStatus");

				var runningServers = _manager.GetRunningServers();

				var result = new
				{
					service = "ServerStream",
					runningServers = runningServers,
					totalRunning = runningServers?.Count() ?? 0,
					checkedAt = DateTime.UtcNow
				};

				LogOperationEnd("GetServersStatus", result);
				return SuccessResponse(result, "Статус серверных подключений получен");

			}, "GetServersStatus");
		}

		/// <summary>
		/// Получение детального статуса конкретного сервера
		/// </summary>
		[HttpGet("status/{protocol}")]
		[RequireStreamRuntime]
		public IActionResult GetServerStatus(string protocol)
		{
			return SafeExecute(() =>
			{
				LogOperationStart("GetServerStatus", new { protocol });

				var validation = ValidateRequest(
					(!string.IsNullOrWhiteSpace(protocol), "Protocol name is required")
				);
				if (validation != null) return validation;

				var runningServers = _manager.GetRunningServers();
				var isRunning = runningServers?.Contains(protocol) ?? false;

				var result = new
				{
					protocol = protocol,
					isRunning = isRunning,
					status = isRunning ? "active" : "inactive",
					checkedAt = DateTime.UtcNow
				};

				LogOperationEnd("GetServerStatus", result);
				return SuccessResponse(result, $"Статус сервера {protocol} получен");

			}, "GetServerStatus");
		}

		/// <summary>
		/// Перезапуск сервера
		/// </summary>
		[HttpPost("restart/{protocol}")]
		[RequireStreamRuntime]
		public async Task<IActionResult> Restart(string protocol)
		{
			return await SafeExecuteAsync(async () =>
			{
				LogOperationStart("RestartServer", new { protocol });

				var validation = ValidateRequest(
					(!string.IsNullOrWhiteSpace(protocol), "Protocol name is required")
				);
				if (validation != null) return validation;

				// Останавливаем сервер
				try
				{
					await _manager.StopServerAsync(protocol, HttpContext.RequestAborted);
					_logger.LogInformation("Server {Protocol} stopped for restart", protocol);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Error stopping server {Protocol} during restart (may not have been running)", protocol);
				}

				// Запускаем сервер
				await _manager.StartServerAsync(protocol, HttpContext.RequestAborted);

				var result = new
				{
					message = $"Сервер {protocol} перезапущен успешно",
					protocol = protocol,
					status = "restarted",
					restartedAt = DateTime.UtcNow
				};

				LogOperationEnd("RestartServer", result);
				return SuccessResponse(result, $"Сервер {protocol} перезапущен");

			}, "RestartServer", $"Server {protocol} restarted successfully", new { protocol });
		}

		/// <summary>
		/// Получение списка поддерживаемых протоколов
		/// </summary>
		[HttpGet("supported-protocols")]
		[RequireStreamRuntime]
		public IActionResult GetSupportedProtocols()
		{
			return SafeExecute(() =>
			{
				var supportedProtocols = new
				{
					protocols = new[] { "tcp", "udp", "websocket", "grpc" },
					descriptions = new Dictionary<string, string>
					{
						{ "tcp", "Transmission Control Protocol - reliable, connection-oriented" },
						{ "udp", "User Datagram Protocol - fast, connectionless" },
						{ "websocket", "WebSocket - full-duplex communication over HTTP" },
						{ "grpc", "gRPC - high-performance RPC framework" }
					}
				};

				return SuccessResponse(supportedProtocols, "Список поддерживаемых протоколов получен");

			}, "GetSupportedProtocols");
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

		private bool IsValidProtocol(string protocol)
		{
			// Добавьте здесь логику валидации поддерживаемых протоколов
			var supportedProtocols = new[] { "tcp", "udp", "websocket", "grpc" };
			return supportedProtocols.Contains(protocol?.ToLowerInvariant());
		}

		#endregion
	}
}
