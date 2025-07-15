using lazy_light_requests_gate.presentation.models.response;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace lazy_light_requests_gate.presentation.controllers
{
	/// <summary>
	/// Базовый контроллер с общей логикой для всех Gateway API контроллеров
	/// </summary>
	[ApiController]
	public abstract class BaseGatewayController : ControllerBase
	{
		protected readonly ILogger _logger;
		private readonly Stopwatch _operationStopwatch;

		protected BaseGatewayController(ILogger logger)
		{
			_logger = logger;
			_operationStopwatch = new Stopwatch();
		}

		#region Success Response Methods

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

		#endregion

		#region Error Response Methods

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
		/// Ответ с ошибкой валидации
		/// </summary>
		protected IActionResult ValidationErrorResponse(string message, object validationErrors = null)
		{
			return ErrorResponse(message, 422, new
			{
				validationErrors,
				type = "ValidationError"
			});
		}

		/// <summary>
		/// Ответ с ошибкой авторизации
		/// </summary>
		protected IActionResult UnauthorizedErrorResponse(string message = "Access denied")
		{
			return ErrorResponse(message, 401, new
			{
				type = "AuthorizationError",
				suggestion = "Please check your credentials and try again"
			});
		}

		/// <summary>
		/// Ответ с ошибкой "не найдено"
		/// </summary>
		protected IActionResult NotFoundErrorResponse(string resource, object identifier = null)
		{
			return ErrorResponse($"{resource} not found", 404, new
			{
				resource,
				identifier,
				type = "NotFoundError"
			});
		}

		#endregion

		#region Exception Handling

		/// <summary>
		/// Обработка исключений с логированием
		/// </summary>
		protected IActionResult HandleException(Exception ex, string operationName, object context = null)
		{
			var requestId = GetRequestId();
			_logger.LogError(ex, "Error in operation {OperationName} for request {RequestId}. Context: {@Context}",
				operationName, requestId, context);

			// Определяем тип ошибки для более точного ответа
			return ex switch
			{
				ArgumentException argEx => ValidationErrorResponse(
					$"Ошибка валидации в операции {operationName}",
					new { parameter = argEx.ParamName, details = argEx.Message }),

				InvalidOperationException invOpEx => ErrorResponse(
					$"Недопустимая операция: {operationName}",
					409,
					new { details = invOpEx.Message, operation = operationName }),

				TimeoutException timeoutEx => ErrorResponse(
					$"Тайм-аут операции {operationName}",
					408,
					new { details = timeoutEx.Message, operation = operationName }),

				UnauthorizedAccessException unAuthEx => UnauthorizedErrorResponse(
					$"Нет доступа к операции {operationName}"),

				_ => ErrorResponse(
					$"Ошибка в операции {operationName}",
					500,
					new
					{
						error = ex.Message,
						operation = operationName,
						context,
						type = ex.GetType().Name
					})
			};
		}

		#endregion

		#region Validation Methods

		/// <summary>
		/// Валидация запроса с возвращением ошибки при провале
		/// </summary>
		protected IActionResult ValidateRequest(params (bool condition, string errorMessage)[] validations)
		{
			foreach (var (condition, errorMessage) in validations)
			{
				if (!condition)
				{
					return ValidationErrorResponse(errorMessage);
				}
			}
			return null;
		}

		/// <summary>
		/// Комплексная валидация с детальными ошибками
		/// </summary>
		protected IActionResult ValidateRequestWithDetails(
			params (bool condition, string field, string errorMessage)[] validations)
		{
			var errors = new List<object>();

			foreach (var (condition, field, errorMessage) in validations)
			{
				if (!condition)
				{
					errors.Add(new { field, message = errorMessage });
				}
			}

			if (errors.Any())
			{
				return ValidationErrorResponse("Validation failed", errors);
			}

			return null;
		}

		#endregion

		#region Safe Execution Methods

		/// <summary>
		/// Безопасное выполнение асинхронной операции с обработкой исключений
		/// </summary>
		public async Task<IActionResult> SafeExecuteAsync<T>(
			Func<Task<T>> action,
			string operationName,
			string successMessage,
			object logData = null)
		{
			_operationStopwatch.Restart();
			try
			{
				_logger.LogInformation("Starting async operation {OperationName} for request {RequestId}",
					operationName, GetRequestId());

				var result = await action();

				_operationStopwatch.Stop();
				_logger.LogInformation("Async operation {OperationName} completed successfully in {ElapsedMs}ms for request {RequestId}",
					operationName, _operationStopwatch.ElapsedMilliseconds, GetRequestId());

				return new JsonResult(result);
			}
			catch (Exception ex)
			{
				_operationStopwatch.Stop();
				_logger.LogError(ex, "Async operation {OperationName} failed after {ElapsedMs}ms for request {RequestId}",
					operationName, _operationStopwatch.ElapsedMilliseconds, GetRequestId());

				return HandleException(ex, operationName, logData);
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
			_operationStopwatch.Restart();
			try
			{
				_logger.LogInformation("Starting operation {OperationName} for request {RequestId}",
					operationName, GetRequestId());

				await operation();

				_operationStopwatch.Stop();
				_logger.LogInformation("Operation {OperationName} completed successfully in {ElapsedMs}ms for request {RequestId}",
					operationName, _operationStopwatch.ElapsedMilliseconds, GetRequestId());

				return SuccessMessage(successMessage ?? $"Операция {operationName} выполнена успешно");
			}
			catch (Exception ex)
			{
				_operationStopwatch.Stop();
				return HandleException(ex, operationName, context);
			}
		}

		/// <summary>
		/// Безопасное выполнение синхронной операции
		/// </summary>
		protected IActionResult SafeExecute<T>(
			Func<T> operation,
			string operationName,
			string successMessage = null,
			object context = null)
		{
			_operationStopwatch.Restart();
			try
			{
				_logger.LogInformation("Starting sync operation {OperationName} for request {RequestId}",
					operationName, GetRequestId());

				var result = operation();

				_operationStopwatch.Stop();
				_logger.LogInformation("Sync operation {OperationName} completed successfully in {ElapsedMs}ms for request {RequestId}",
					operationName, _operationStopwatch.ElapsedMilliseconds, GetRequestId());

				return new JsonResult(result);
			}
			catch (Exception ex)
			{
				_operationStopwatch.Stop();
				return HandleException(ex, operationName, context);
			}
		}

		#endregion

		#region Logging Methods

		/// <summary>
		/// Получить ID запроса для трассировки
		/// </summary>
		protected string GetRequestId()
		{
			return HttpContext.TraceIdentifier ?? Guid.NewGuid().ToString();
		}

		/// <summary>
		/// Логирование начала операции
		/// </summary>
		protected void LogOperationStart(string operationName, object parameters = null)
		{
			_operationStopwatch.Restart();
			_logger.LogInformation("=== {OperationName} Started === RequestId: {RequestId}, Parameters: {@Parameters}",
				operationName, GetRequestId(), parameters);
		}

		/// <summary>
		/// Логирование завершения операции
		/// </summary>
		protected void LogOperationEnd(string operationName, object result = null)
		{
			_operationStopwatch.Stop();
			_logger.LogInformation("=== {OperationName} Completed in {ElapsedMs}ms === RequestId: {RequestId}, Result: {@Result}",
				operationName, _operationStopwatch.ElapsedMilliseconds, GetRequestId(), result);
		}

		/// <summary>
		/// Логирование предупреждения операции
		/// </summary>
		protected void LogOperationWarning(string operationName, string message, object details = null)
		{
			_logger.LogWarning("Operation {OperationName} warning for request {RequestId}: {Message}. Details: {@Details}",
				operationName, GetRequestId(), message, details);
		}

		#endregion

		#region Performance & Metrics

		/// <summary>
		/// Выполнение операции с измерением производительности
		/// </summary>
		protected async Task<IActionResult> ExecuteWithMetrics<T>(
			Func<Task<T>> operation,
			string operationName,
			object context = null)
		{
			var stopwatch = Stopwatch.StartNew();
			var requestId = GetRequestId();

			try
			{
				_logger.LogInformation("Starting metered operation {OperationName} for request {RequestId}",
					operationName, requestId);

				var result = await operation();

				stopwatch.Stop();

				_logger.LogInformation("Metered operation {OperationName} completed successfully. " +
					"Duration: {Duration}ms, RequestId: {RequestId}",
					operationName, stopwatch.ElapsedMilliseconds, requestId);

				return SuccessWithMetadata(result, new
				{
					performance = new
					{
						operationName,
						durationMs = stopwatch.ElapsedMilliseconds,
						requestId
					}
				});
			}
			catch (Exception ex)
			{
				stopwatch.Stop();

				_logger.LogError(ex, "Metered operation {OperationName} failed after {Duration}ms for request {RequestId}",
					operationName, stopwatch.ElapsedMilliseconds, requestId);

				return HandleException(ex, operationName, context);
			}
		}

		/// <summary>
		/// Получение базовых метрик контроллера
		/// </summary>
		protected object GetControllerMetrics()
		{
			return new
			{
				requestId = GetRequestId(),
				timestamp = DateTime.UtcNow,
				controller = GetType().Name,
				userAgent = Request.Headers.UserAgent.ToString(),
				method = Request.Method,
				path = Request.Path,
				queryString = Request.QueryString.ToString()
			};
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Проверка, является ли запрос AJAX
		/// </summary>
		protected bool IsAjaxRequest()
		{
			return Request.Headers["X-Requested-With"] == "XMLHttpRequest";
		}

		/// <summary>
		/// Получение IP адреса клиента
		/// </summary>
		protected string GetClientIpAddress()
		{
			return Request.Headers["X-Forwarded-For"].FirstOrDefault()
				?? Request.Headers["X-Real-IP"].FirstOrDefault()
				?? HttpContext.Connection.RemoteIpAddress?.ToString()
				?? "unknown";
		}

		/// <summary>
		/// Создание ответа с кастомным статусом и заголовками
		/// </summary>
		protected IActionResult CustomResponse<T>(
			T data,
			int statusCode,
			string message = null,
			Dictionary<string, string> headers = null)
		{
			var response = new ApiResponse<T>
			{
				Success = statusCode >= 200 && statusCode < 300,
				Data = data,
				Message = message,
				Timestamp = DateTime.UtcNow,
				RequestId = GetRequestId()
			};

			if (headers != null)
			{
				foreach (var header in headers)
				{
					Response.Headers.Append(header.Key, header.Value);
				}
			}

			return StatusCode(statusCode, response);
		}

		#endregion
	}
}
