using lazy_light_requests_gate.infrastructure.networking;
using lazy_light_requests_gate.presentation.attributes;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.controllers
{
	/// <summary>
	/// Контроллер для управления клиентскими stream подключениями
	/// </summary>
	[ApiController]
	[Route("api/[controller]")]
	public class ClientStreamController : BaseGatewayController
	{
		private readonly NetworkClientManager _manager;

		public ClientStreamController(
			NetworkClientManager manager,
			ILogger<ClientStreamController> logger) : base(logger)
		{
			_manager = manager;
		}

		/// <summary>
		/// Запуск клиента для указанного протокола
		/// </summary>
		[HttpPost("start/{protocol}")]
		[RequireStreamRuntime]
		public async Task<IActionResult> Start(string protocol)
		{
			return await SafeExecuteAsync(async () =>
			{
				LogOperationStart("StartClient", new { protocol });

				// Валидация протокола
				var validation = ValidateRequest(
					(!string.IsNullOrWhiteSpace(protocol), "Protocol name is required"),
					(IsValidProtocol(protocol), $"Unsupported protocol: {protocol}")
				);
				if (validation != null) return validation;

				await _manager.StartClientAsync(protocol, HttpContext.RequestAborted);

				var result = new
				{
					message = $"Клиент {protocol} запущен успешно",
					protocol = protocol,
					status = "started",
					startedAt = DateTime.UtcNow
				};

				LogOperationEnd("StartClient", result);
				return SuccessResponse(result, $"Клиент {protocol} запущен");

			}, "StartClient", $"Client {protocol} started successfully", new { protocol });
		}

		/// <summary>
		/// Остановка клиента для указанного протокола
		/// </summary>
		[HttpPost("stop/{protocol}")]
		[RequireStreamRuntime]
		public async Task<IActionResult> Stop(string protocol)
		{
			return await SafeExecuteAsync(async () =>
			{
				LogOperationStart("StopClient", new { protocol });

				// Валидация протокола
				var validation = ValidateRequest(
					(!string.IsNullOrWhiteSpace(protocol), "Protocol name is required")
				);
				if (validation != null) return validation;

				await _manager.StopClientAsync(protocol, HttpContext.RequestAborted);

				var result = new
				{
					message = $"Клиент {protocol} остановлен успешно",
					protocol = protocol,
					status = "stopped",
					stoppedAt = DateTime.UtcNow
				};

				LogOperationEnd("StopClient", result);
				return SuccessResponse(result, $"Клиент {protocol} остановлен");

			}, "StopClient", $"Client {protocol} stopped successfully", new { protocol });
		}

		/// <summary>
		/// Получение статуса всех клиентских подключений
		/// </summary>
		[HttpGet("status")]
		[RequireStreamRuntime]
		public IActionResult Status()
		{
			return SafeExecute(() =>
			{
				LogOperationStart("GetClientsStatus");

				var runningClients = _manager.GetRunningClients();

				var result = new
				{
					service = "ClientStream",
					runningClients = runningClients,
					totalRunning = runningClients?.Count() ?? 0,
					checkedAt = DateTime.UtcNow
				};

				LogOperationEnd("GetClientsStatus", result);
				return SuccessResponse(result, "Статус клиентских подключений получен");

			}, "GetClientsStatus");
		}

		/// <summary>
		/// Получение детального статуса конкретного клиента
		/// </summary>
		[HttpGet("status/{protocol}")]
		[RequireStreamRuntime]
		public IActionResult GetClientStatus(string protocol)
		{
			return SafeExecute(() =>
			{
				LogOperationStart("GetClientStatus", new { protocol });

				var validation = ValidateRequest(
					(!string.IsNullOrWhiteSpace(protocol), "Protocol name is required")
				);
				if (validation != null) return validation;

				var runningClients = _manager.GetRunningClients();
				var isRunning = runningClients?.Contains(protocol) ?? false;

				var result = new
				{
					protocol = protocol,
					isRunning = isRunning,
					status = isRunning ? "active" : "inactive",
					checkedAt = DateTime.UtcNow
				};

				LogOperationEnd("GetClientStatus", result);
				return SuccessResponse(result, $"Статус клиента {protocol} получен");

			}, "GetClientStatus");
		}

		/// <summary>
		/// Перезапуск клиента
		/// </summary>
		[HttpPost("restart/{protocol}")]
		[RequireStreamRuntime]
		public async Task<IActionResult> Restart(string protocol)
		{
			return await SafeExecuteAsync(async () =>
			{
				LogOperationStart("RestartClient", new { protocol });

				var validation = ValidateRequest(
					(!string.IsNullOrWhiteSpace(protocol), "Protocol name is required")
				);
				if (validation != null) return validation;

				// Останавливаем клиента
				try
				{
					await _manager.StopClientAsync(protocol, HttpContext.RequestAborted);
					_logger.LogInformation("Client {Protocol} stopped for restart", protocol);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Error stopping client {Protocol} during restart (may not have been running)", protocol);
				}

				// Запускаем клиента
				await _manager.StartClientAsync(protocol, HttpContext.RequestAborted);

				var result = new
				{
					message = $"Клиент {protocol} перезапущен успешно",
					protocol = protocol,
					status = "restarted",
					restartedAt = DateTime.UtcNow
				};

				LogOperationEnd("RestartClient", result);
				return SuccessResponse(result, $"Клиент {protocol} перезапущен");

			}, "RestartClient", $"Client {protocol} restarted successfully", new { protocol });
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
