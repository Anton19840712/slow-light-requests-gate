using application.interfaces.messaging;
using domain.entities;
using infrastructure.messaging;
using infrastructure.persistence.repositories;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.temp.presentation.controllers
{
	[ApiController]
	[Route("api/admin")]
	public class AdminController : ControllerBase
	{
		private readonly ILogger<AdminController> _logger;
		private readonly IRabbitMqQueueListener _queueListener;
		private readonly MongoRepository<QueuesEntity> _queuesRepository;

		public AdminController(
			ILogger<AdminController> logger,
			IRabbitMqQueueListener queueListener,
			MongoRepository<QueuesEntity> queuesRepository)
		{
			_logger = logger;
			_queueListener = queueListener;
			_queuesRepository = queuesRepository;
		}

		/// <summary>
		/// Получить все сообщения из всех очередей реббит
		/// </summary>
		[HttpGet("consume")]
		public async Task<IActionResult> ConsumeAllQueues(CancellationToken cancellationToken)
		{
			try
			{
				_logger.LogInformation("Dumping messages from all queues.");

				var elements = await _queuesRepository.GetAllAsync();

				foreach (var element in elements)
				{
					try
					{
						await _queueListener.StartListeningAsync(element.OutQueueName, cancellationToken);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error retrieving messages from queue: {QueueName}", element.OutQueueName);
					}
				}

				_logger.LogInformation("Процесс получения сообщений из очередей завершен.");
				return Ok();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while getting messages from queues");
				return Problem(ex.Message);
			}
		}
	}
}
