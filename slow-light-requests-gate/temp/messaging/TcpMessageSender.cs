using System.Net.Sockets;
using System.Text;
using application.interfaces.messaging;

namespace infrastructure.messaging
{
	public class TcpMessageSender : BaseMessageSender<TcpMessageSender>
	{
		private readonly TcpClient _tcpClient;

		public TcpMessageSender(
						TcpClient tcpClient,
						IRabbitMqQueueListener rabbitMqQueueListener,
						ILogger<TcpMessageSender> logger) : base(rabbitMqQueueListener, logger)
		{
			_tcpClient = tcpClient;
		}

		protected override async Task SendToClientAsync(string message, CancellationToken cancellationToken)
		{
			if (_tcpClient.Client.Poll(0, SelectMode.SelectRead) && _tcpClient.Client.Available == 0)
			{
				_logger.LogWarning("TCP-клиент отключился.");
				return;
			}

			byte[] data = Encoding.UTF8.GetBytes(message);
			var stream = _tcpClient.GetStream();
			await stream.WriteAsync(data, 0, data.Length, cancellationToken);
			await stream.FlushAsync(cancellationToken);
		}
	}
}
