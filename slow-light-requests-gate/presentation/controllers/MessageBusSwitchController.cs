using lazy_light_requests_gate.core.application.interfaces.buses;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.сontrollers;

[ApiController]
[Route("api/[controller]")]
public class MessageBusSwitchController : ControllerBase
{
	private readonly IMessageBusServiceFactory _messageBusServiceFactory;
	private readonly ILogger<MessageBusSwitchController> _logger;

	public MessageBusSwitchController(
		IMessageBusServiceFactory messageBusServiceFactory,
		ILogger<MessageBusSwitchController> logger)
	{
		_messageBusServiceFactory = messageBusServiceFactory;
		_logger = logger;
	}

	/// <summary>
	/// Переключение шины сообщений
	/// </summary>
	[HttpPost("switch")]
	public async Task<IActionResult> SwitchMessageBus([FromBody] MessageBusSwitchRequest request)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(request.BusType))
			{
				return BadRequest("Bus type is required");
			}

			var busType = request.BusType.ToLower();
			if (!IsValidBusType(busType)) // Обновленная валидация
			{
				return BadRequest("Supported bus types: 'rabbit', 'activemq', 'pulsar', 'kafka'");
			}

			var previousBusType = _messageBusServiceFactory.GetCurrentBusType();

			// Тестируем подключение к новой шине перед переключением
			var testBusService = _messageBusServiceFactory.CreateMessageBusService(busType);
			var connectionTest = await testBusService.TestConnectionAsync();

			if (!connectionTest)
			{
				_logger.LogWarning("Connection test failed for bus type: {BusType}", busType);
				return BadRequest($"Failed to connect to {busType}. Please check configuration and ensure the message bus is available.");
			}

			// Переключаем шину
			_messageBusServiceFactory.SetDefaultBusType(busType);

			_logger.LogInformation("Message bus switched from {PreviousBusType} to {NewBusType}", previousBusType, busType);

			return Ok(new
			{
				message = $"Message bus switched from {previousBusType} to {busType}",
				previousBus = previousBusType,
				currentBus = _messageBusServiceFactory.GetCurrentBusType(),
				connectionTestPassed = true,
				timestamp = DateTime.UtcNow
			});
		}
		catch (NotSupportedException ex)
		{
			_logger.LogError(ex, "Unsupported bus type requested: {BusType}", request.BusType);
			return BadRequest(ex.Message);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error switching message bus to: {BusType}", request.BusType);
			return StatusCode(500, new
			{
				message = "Internal server error while switching message bus",
				error = ex.Message
			});
		}
	}

	/// <summary>
	/// Получение текущей шины сообщений
	/// </summary>
	[HttpGet("current")]
	public async Task<IActionResult> GetCurrentMessageBus()
	{
		try
		{
			var currentBusType = _messageBusServiceFactory.GetCurrentBusType();
			var connectionTest = await _messageBusServiceFactory.TestCurrentBusConnectionAsync();

			return Ok(new
			{
				currentBus = currentBusType,
				connectionStatus = connectionTest ? "Connected" : "Disconnected",
				timestamp = DateTime.UtcNow
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting current message bus status");
			return StatusCode(500, new
			{
				message = "Error getting current message bus status",
				error = ex.Message
			});
		}
	}

	/// <summary>
	/// Тестирование подключения к указанной шине
	/// </summary>
	[HttpPost("test")]
	public async Task<IActionResult> TestMessageBusConnection([FromBody] MessageBusTestRequest request)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(request.BusType))
			{
				return BadRequest("Bus type is required");
			}

			var busType = request.BusType.ToLower();
			if (!IsValidBusType(busType)) // Обновленная валидация
			{
				return BadRequest("Supported bus types: 'rabbit', 'activemq', 'pulsar', 'kafka'");
			}

			var busService = _messageBusServiceFactory.CreateMessageBusService(busType);
			var connectionTest = await busService.TestConnectionAsync();

			return Ok(new
			{
				busType,
				connectionStatus = connectionTest ? "Success" : "Failed",
				isCurrentBus = busType == _messageBusServiceFactory.GetCurrentBusType(),
				timestamp = DateTime.UtcNow
			});
		}
		catch (NotSupportedException ex)
		{
			return BadRequest(ex.Message);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error testing connection to bus type: {BusType}", request.BusType);
			return StatusCode(500, new
			{
				message = "Error testing message bus connection",
				error = ex.Message
			});
		}
	}

	/// <summary>
	/// Получение списка поддерживаемых шин сообщений (обновленный с поддержкой Tarantool)
	/// </summary>
	[HttpGet("supported")]
	public IActionResult GetSupportedMessageBuses()
	{
		return Ok(new
		{
			supportedBuses = new[] { "rabbit", "activemq", "pulsar", "tarantool", "kafkastreams" },
			currentBus = _messageBusServiceFactory.GetCurrentBusType(),
			timestamp = DateTime.UtcNow
		});
	}

	// Вспомогательный метод для валидации типов шин
	private static bool IsValidBusType(string busType)
	{
		return busType is "rabbit" or "activemq" or "pulsar" or "tarantool" or "kafkastreams";
	}

	/// <summary>
	/// Публикация тестового сообщения в текущую шину
	/// </summary>
	[HttpPost("publish-test")]
	public async Task<IActionResult> PublishTestMessage([FromBody] TestMessageRequest request)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(request.QueueName))
			{
				return BadRequest("Queue name is required");
			}

			var currentBusType = _messageBusServiceFactory.GetCurrentBusType();
			var busService = _messageBusServiceFactory.CreateMessageBusService(currentBusType);

			var testMessage = request.Message ?? $"Test message from API at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
			var routingKey = request.RoutingKey ?? request.QueueName;

			await busService.PublishMessageAsync(request.QueueName, routingKey, testMessage);

			_logger.LogInformation("Test message published to {BusType} queue: {QueueName}", currentBusType, request.QueueName);

			return Ok(new
			{
				message = "Test message published successfully",
				busType = currentBusType,
				queueName = request.QueueName,
				routingKey,
				publishedMessage = testMessage,
				timestamp = DateTime.UtcNow
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error publishing test message to queue: {QueueName}", request.QueueName);
			return StatusCode(500, new
			{
				message = "Error publishing test message",
				error = ex.Message
			});
		}
	}
}

public class MessageBusSwitchRequest
{
	public string BusType { get; set; }
}

public class MessageBusTestRequest
{
	public string BusType { get; set; }
}

public class TestMessageRequest
{
	public string QueueName { get; set; }
	public string RoutingKey { get; set; }
	public string Message { get; set; }
}