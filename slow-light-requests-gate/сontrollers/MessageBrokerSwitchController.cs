using lazy_light_requests_gate.messaging;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.сontrollers
{
	[ApiController]
	[Route("api/[controller]")]
	public class MessageBrokerSwitchController : ControllerBase
	{
		private readonly IMessageBrokerFactory _messageBrokerFactory;
		private readonly ILogger<MessageBrokerSwitchController> _logger;

		public MessageBrokerSwitchController(
			IMessageBrokerFactory messageBrokerFactory,
			ILogger<MessageBrokerSwitchController> logger)
		{
			_messageBrokerFactory = messageBrokerFactory;
			_logger = logger;
		}

		[HttpPost("switch")]
		public IActionResult SwitchMessageBroker([FromBody] MessageBrokerSwitchRequest request)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(request.BrokerType))
				{
					return BadRequest("Message broker type is required");
				}

				var brokerType = request.BrokerType.ToLower();
				if (brokerType != "kafka" && brokerType != "rabbitmq")
				{
					return BadRequest("Supported message broker types: 'kafka', 'rabbitmq'");
				}

				_messageBrokerFactory.SetDefaultBrokerType(brokerType);

				_logger.LogInformation("Message broker switched to: {BrokerType}", brokerType);

				return Ok(new
				{
					message = $"Message broker switched to {brokerType}",
					currentBroker = _messageBrokerFactory.GetCurrentBrokerType()
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error switching message broker");
				return StatusCode(500, "Internal server error");
			}
		}

		[HttpGet("current")]
		public IActionResult GetCurrentMessageBroker()
		{
			return Ok(new
			{
				currentBroker = _messageBrokerFactory.GetCurrentBrokerType()
			});
		}
	}

	public class MessageBrokerSwitchRequest
	{
		public string BrokerType { get; set; }
	}
}
