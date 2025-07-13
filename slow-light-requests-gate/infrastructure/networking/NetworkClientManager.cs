using lazy_light_requests_gate.core.application.interfaces.networking;

namespace lazy_light_requests_gate.infrastructure.networking
{
	// 2
	public class NetworkClientManager
	{
		private readonly IEnumerable<INetworkClient> _clients;
		private readonly Dictionary<string, INetworkClient> _runningClients = new();

		public NetworkClientManager(IEnumerable<INetworkClient> clients)
		{
			_clients = clients;
		}

		public async Task StartClientAsync(string protocol, CancellationToken cancellationToken)
		{
			if (_runningClients.ContainsKey(protocol)) return;

			var client = _clients.FirstOrDefault(c => c.Protocol.Equals(protocol, StringComparison.OrdinalIgnoreCase));
			if (client == null)
				throw new InvalidOperationException($"Клиент с протоколом {protocol} не найден.");

			await client.StartAsync(cancellationToken);
			_runningClients[protocol] = client;
		}

		public async Task StopClientAsync(string protocol, CancellationToken cancellationToken)
		{
			if (_runningClients.TryGetValue(protocol, out var client))
			{
				await client.StopAsync(cancellationToken);
				_runningClients.Remove(protocol);
			}
		}
		public IReadOnlyCollection<string> GetRunningClients() => _runningClients.Keys.ToList();
	}
}
