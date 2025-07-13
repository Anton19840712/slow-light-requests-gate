using System.Net.WebSockets;
using System.Text;
using lazy_light_requests_gate.core.application.interfaces.listeners;
using lazy_light_requests_gate.core.application.services.messaging;

namespace lazy_light_requests_gate.infrastructure.messaging
{
	public class WebSocketMessageSender : BaseMessageSender<WebSocketMessageSender>
	{
		private readonly WebSocket _socket;

		public WebSocketMessageSender(
						WebSocket socket,
						IRabbitMqQueueListener rabbitMqQueueListener,
						ILogger<WebSocketMessageSender> logger) : base(rabbitMqQueueListener, logger)
		{
			_socket = socket;
		}

		protected override async Task SendToClientAsync(string message, CancellationToken cancellationToken)
		{
			byte[] buffer = Encoding.UTF8.GetBytes(message);
			await _socket.SendAsync(
				new ArraySegment<byte>(buffer),
				WebSocketMessageType.Text,
				endOfMessage: true,
				cancellationToken);
		}
	}
}
