using lazy_light_requests_gate.core.application.helpers;
using lazy_light_requests_gate.core.application.interfaces.buses;
using lazy_light_requests_gate.presentation.models.common;
using lazy_light_requests_gate.presentation.models.request;
using lazy_light_requests_gate.temp.apptypeswitcher;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class MessageBusSwitchController : ControllerBase
	{
		private readonly IMessageBusServiceFactory _messageBusServiceFactory;
		private readonly ILogger<MessageBusSwitchController> _logger;
		private readonly IDynamicBusManager _busManager;
		private readonly IConfiguration _configuration;

		public MessageBusSwitchController(
			IMessageBusServiceFactory messageBusServiceFactory,
			IDynamicBusManager busManager,
			IConfiguration configuration,
			ILogger<MessageBusSwitchController> logger)
		{
			_busManager = busManager;
			_messageBusServiceFactory = messageBusServiceFactory;
			_configuration = configuration;
			_logger = logger;
		}

		/// <summary>
		/// Переключение между типами шин сообщений
		/// </summary>
		[HttpPost("switch")]
		[RequireEitherAPIRuntime]
		public async Task<IActionResult> SwitchBus([FromBody] BusSwitchRequest request)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(request.BusType))
				{
					return BadRequest("Bus type is required");
				}

				var busType = BusTypeHelper.Normalize(request.BusType);

				if (!BusTypeHelper.IsValidBusType(busType))
				{
					return BadRequest("Supported bus types: 'rabbit', 'activemq', 'pulsar', 'kafkastreams', 'tarantool'");
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
		[RequireEitherAPIRuntime]
		public async Task<IActionResult> GetCurrentBus()
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
		/// Динамическое переподключение к шине с новыми параметрами
		/// </summary>
		[HttpPost("reconnect")]
		[RequireEitherAPIRuntime]
		public async Task<IActionResult> ReconnectWithNewParameters([FromBody] BusReconnectRequest request)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(request.BusType))
					return BadRequest("Bus type is required");

				// Нормализуем тип шины сразу
				var busType = BusTypeHelper.Normalize(request.BusType);

				if (!BusTypeHelper.IsValidBusType(busType))
					return BadRequest("Supported bus types: 'rabbit', 'activemq', 'pulsar', 'kafkastreams', 'tarantool'");

				// Переподключаемся с новыми параметрами
				await _busManager.ReconnectWithNewParametersAsync(busType, request.ConnectionParameters);

				// Устанавливаем нормализованный тип шины
				_messageBusServiceFactory.SetDefaultBusType(busType);

				_logger.LogInformation("Message bus reconnected to: {BusType} with new parameters", busType);

				return Ok(new
				{
					message = $"Message bus reconnected to {busType} with new parameters",
					currentBus = _messageBusServiceFactory.GetCurrentBusType(),
					connectionInfo = await _busManager.GetCurrentConnectionInfoAsync(),
					timestamp = DateTime.UtcNow
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error reconnecting message bus with new parameters");
				return StatusCode(500, new { message = "Internal server error", details = ex.Message });
			}
		}

		/// <summary>
		/// Получение информации о текущем подключении
		/// </summary>
		[HttpGet("connection-info")]
		[RequireEitherAPIRuntime]
		public async Task<IActionResult> GetConnectionInfo()
		{
			try
			{
				var connectionInfo = await _busManager.GetCurrentConnectionInfoAsync();
				return Ok(connectionInfo);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting connection info");
				return StatusCode(500, new
				{
					message = "Error getting connection info",
					error = ex.Message
				});
			}
		}

		/// <summary>
		/// Получение списка поддерживаемых шин сообщений
		/// </summary>
		[HttpGet("supported")]
		[RequireEitherAPIRuntime]
		public IActionResult GetSupportedBuses()
		{
			return Ok(new
			{
				supportedBuses = new[] { "rabbit", "activemq", "pulsar", "tarantool", "kafkastreams" },
				currentBus = _messageBusServiceFactory.GetCurrentBusType(),
				timestamp = DateTime.UtcNow
			});
		}

		/// <summary>
		/// Публикация тестового сообщения
		/// </summary>
		[HttpPost("publish-test")]
		[RequireEitherAPIRuntime]
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
}
