using lazy_light_requests_gate.core.application.interfaces.listeners;
using lazy_light_requests_gate.core.application.interfaces.messaging;
using lazy_light_requests_gate.core.application.interfaces.networking;
using lazy_light_requests_gate.infrastructure.messaging;
using lazy_light_requests_gate.infrastructure.networking;

namespace lazy_light_requests_gate.core.application.services.messaging
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
