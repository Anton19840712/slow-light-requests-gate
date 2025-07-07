using System.Net.Sockets;
using application.interfaces.networking;

namespace infrastructure.networking
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
