using application.interfaces.networking;
using application.interfaces.services;
using infrastructure.networking;
using lazy_light_requests_gate.core.application.interfaces.listeners;

namespace infrastructure.messaging
{
	public class ConnectionMessageSenderFactory
	{
		private readonly IRabbitMqQueueListener _rabbitMqQueueListener;
		private readonly ILoggerFactory _loggerFactory;

		public ConnectionMessageSenderFactory(IRabbitMqQueueListener rabbitMqQueueListener, ILoggerFactory loggerFactory)
		{
			_rabbitMqQueueListener = rabbitMqQueueListener;
			_loggerFactory = loggerFactory;
		}

		public IConnectionMessageSender CreateSender(IConnectionContext connectionContext)
		{
			switch (connectionContext)
			{
				case TcpConnectionContext tcpContext:
					return new TcpMessageSender(tcpContext.TcpClient, _rabbitMqQueueListener, _loggerFactory.CreateLogger<TcpMessageSender>());

				case UdpConnectionContext udpContext:
					return new UdpMessageSender(udpContext.UdpClient, udpContext.RemoteEndPoint, _rabbitMqQueueListener, _loggerFactory.CreateLogger<UdpMessageSender>());

				case WebSocketConnectionContext webSocketContext:
					return new WebSocketMessageSender(webSocketContext.Socket, _rabbitMqQueueListener, _loggerFactory.CreateLogger<WebSocketMessageSender>());

				default:
					throw new NotSupportedException("Unsupported connection type.");
			}
		}
	}
}
