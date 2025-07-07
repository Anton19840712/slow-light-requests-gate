using System.Net.Sockets;
using System.Net;
using application.interfaces.networking;

namespace infrastructure.networking
{
	public class UdpConnectionContext : IConnectionContext
	{
		public UdpClient UdpClient { get; }
		public IPEndPoint RemoteEndPoint { get; }

		public UdpConnectionContext(UdpClient udpClient, IPEndPoint remoteEndPoint)
		{
			UdpClient = udpClient;
			RemoteEndPoint = remoteEndPoint;
		}

		public string Protocol => "udp";
	}
}
