using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.controllers
{
	/// <summary>
	/// Базовый контроллер с общей логикой для всех Gateway API контроллеров
	/// </summary>
	[ApiController]
	public abstract class BaseGatewayController : ControllerBase
	{
		protected readonly ILogger _logger;

		protected BaseGatewayController(ILogger logger)
		{
			_logger = logger;
		}

		/// <summary>
		/// Стандартизированный успешный ответ
		/// </summary>
		protected IActionResult SuccessResponse<T>(T data, string message = null)
		{
			return Ok(new ApiResponse<T>
			{
				Success = true,
				Data = data,
				Message = message,
				Timestamp = DateTime.UtcNow,
				RequestId = GetRequestId()
			});
		}

		/// <summary>
		/// Стандартизированный ответ с ошибкой
		/// </summary>
		protected IActionResult ErrorResponse(string message, int statusCode = 400, object details = null)
		{
			var response = new ApiResponse<object>
			{
				Success = false,
				Message = message,
				Data = details,
				Timestamp = DateTime.UtcNow,
				RequestId = GetRequestId()
			};

			return StatusCode(statusCode, response);
		}

		/// <summary>
		/// Обработка исключений с логированием
		/// </summary>
		protected IActionResult HandleException(Exception ex, string operationName, object context = null)
		{
			var requestId = GetRequestId();
			_logger.LogError(ex, "Error in operation {OperationName} for request {RequestId}. Context: {@Context}",
				operationName, requestId, context);

			return ErrorResponse(
				$"Ошибка в операции {operationName}",
				500,
				new
				{
					error = ex.Message,
					operation = operationName,
					context
				}
			);
		}

		/// <summary>
		/// Простой успешный ответ для операций без возвращаемых данных
		/// </summary>
		protected IActionResult SuccessMessage(string message)
		{
			return SuccessResponse(new { }, message);
		}

		/// <summary>
		/// Ответ с данными и дополнительной информацией
		/// </summary>
		protected IActionResult SuccessWithMetadata<T>(T data, object metadata, string message = null)
		{
			return SuccessResponse(new
			{
				data,
				metadata
			}, message);
		}

		/// <summary>
		/// Валидация запроса с возвращением ошибки при провале
		/// </summary>
		protected IActionResult ValidateRequest(params (bool condition, string errorMessage)[] validations)
		{
			foreach (var (condition, errorMessage) in validations)
			{
				if (!condition)
				{
					return ErrorResponse(errorMessage, 400);
				}
			}
			return null;
		}

		/// <summary>
		/// Безопасное выполнение операции с обработкой исключений
		/// </summary>
		protected async Task<IActionResult> SafeExecuteAsync<T>(
			Func<Task<T>> operation,
			string operationName,
			string successMessage = null,
			object context = null)
		{
			try
			{
				_logger.LogInformation("Starting operation {OperationName} for request {RequestId}",
					operationName, GetRequestId());

				var result = await operation();

				_logger.LogInformation("Operation {OperationName} completed successfully for request {RequestId}",
					operationName, GetRequestId());

				return SuccessResponse(result, successMessage);
			}
			catch (Exception ex)
			{
				return HandleException(ex, operationName, context);
			}
		}

		/// <summary>
		/// Безопасное выполнение операции без возвращаемого значения
		/// </summary>
		protected async Task<IActionResult> SafeExecuteAsync(
			Func<Task> operation,
			string operationName,
			string successMessage = null,
			object context = null)
		{
			try
			{
				_logger.LogInformation("Starting operation {OperationName} for request {RequestId}",
					operationName, GetRequestId());

				await operation();

				_logger.LogInformation("Operation {OperationName} completed successfully for request {RequestId}",
					operationName, GetRequestId());

				return SuccessMessage(successMessage ?? $"Операция {operationName} выполнена успешно");
			}
			catch (Exception ex)
			{
				return HandleException(ex, operationName, context);
			}
		}

		/// <summary>
		/// Получить ID запроса для трассировки
		/// </summary>
		private string GetRequestId()
		{
			return HttpContext.TraceIdentifier ?? Guid.NewGuid().ToString();
		}

		/// <summary>
		/// Логирование начала операции
		/// </summary>
		protected void LogOperationStart(string operationName, object parameters = null)
		{
			_logger.LogInformation("=== {OperationName} Started === RequestId: {RequestId}, Parameters: {@Parameters}",
				operationName, GetRequestId(), parameters);
		}

		/// <summary>
		/// Логирование завершения операции
		/// </summary>
		protected void LogOperationEnd(string operationName, object result = null)
		{
			_logger.LogInformation("=== {OperationName} Completed === RequestId: {RequestId}, Result: {@Result}",
				operationName, GetRequestId(), result);
		}
	}

	/// <summary>
	/// Стандартизированная модель ответа API
	/// </summary>
	public class ApiResponse<T>
	{
		public bool Success { get; set; }
		public string Message { get; set; }
		public T Data { get; set; }
		public DateTime Timestamp { get; set; }
		public string RequestId { get; set; }
	}
}