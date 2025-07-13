using System.Net.Sockets;
using System.Net;
using lazy_light_requests_gate.core.application.interfaces.networking;

namespace lazy_light_requests_gate.infrastructure.networking
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
