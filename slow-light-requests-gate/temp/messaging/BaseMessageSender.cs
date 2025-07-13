using application.interfaces.services;
using lazy_light_requests_gate.core.application.interfaces.listeners;

namespace infrastructure.messaging
{
	public abstract class BaseMessageSender<T> : IConnectionMessageSender
	{
		protected readonly IRabbitMqQueueListener _rabbitMqQueueListener;
		protected readonly ILogger<T> _logger;

		protected BaseMessageSender(
			IRabbitMqQueueListener rabbitMqQueueListener,
			ILogger<T> logger)
		{
			_rabbitMqQueueListener = rabbitMqQueueListener;
			_logger = logger;
		}

		public async Task SendMessageAsync(string queueForListening, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(queueForListening))
			{
				_logger.LogWarning("Имя очереди для прослушивания не указано.");
				return;
			}

			if (_rabbitMqQueueListener == null)
			{
				_logger.LogError("RabbitMQ listener не инициализирован.");
				return;
			}

			try
			{
				await _rabbitMqQueueListener.StartListeningAsync(
					queueOutName: queueForListening,
					stoppingToken: cancellationToken,
					onMessageReceived: async message =>
					{
						if (message == null)
						{
							_logger.LogWarning("Получено пустое сообщение из очереди.");
							return;
						}

						try
						{
							await SendToClientAsync(message + "\n", cancellationToken);
							_logger.LogInformation("Сообщение отправлено клиенту: {Message}", message);
						}
						catch (Exception ex)
						{
							_logger.LogError(ex, "Ошибка при отправке сообщения клиенту");
						}
					});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Ошибка в процессе отправки сообщений клиенту");
			}
		}
		protected abstract Task SendToClientAsync(string message, CancellationToken cancellationToken);
	}
}
