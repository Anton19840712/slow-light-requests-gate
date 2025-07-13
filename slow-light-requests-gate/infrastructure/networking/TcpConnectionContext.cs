using System.Net.Sockets;
using lazy_light_requests_gate.core.application.interfaces.networking;

namespace lazy_light_requests_gate.infrastructure.networking
{
	public class TcpConnectionContext : IConnectionContext
	{
		public TcpClient TcpClient { get; }

		public TcpConnectionContext(TcpClient tcpClient)
		{
			TcpClient = tcpClient;
		}

		public string Protocol => "tcp";
	}
}
