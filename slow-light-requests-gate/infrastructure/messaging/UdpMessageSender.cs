using System.Net.Sockets;
using System.Net;
using System.Text;
using lazy_light_requests_gate.core.application.interfaces.listeners;
using lazy_light_requests_gate.infrastructure.services.messaging;

namespace lazy_light_requests_gate.infrastructure.messaging
{
	public class UdpMessageSender : BaseMessageSender<UdpMessageSender>
	{
		private readonly UdpClient _udpClient;
		private readonly IPEndPoint _remoteEndPoint;

		public UdpMessageSender(
						UdpClient udpClient,
						IPEndPoint remoteEndPoint,
						IRabbitMqQueueListener rabbitMqQueueListener,
						ILogger<UdpMessageSender> logger) : base(rabbitMqQueueListener, logger)
		{
			_udpClient = udpClient;
			_remoteEndPoint = remoteEndPoint;
		}

		protected override async Task SendToClientAsync(string message, CancellationToken cancellationToken)
		{
			byte[] data = Encoding.UTF8.GetBytes(message);
			await _udpClient.SendAsync(data, data.Length, _remoteEndPoint);
		}
	}
}
