using lazy_light_requests_gate.core.application.interfaces.networking;

namespace lazy_light_requests_gate.infrastructure.networking
{
	// 2
	public class NetworkServerManager
	{
		private readonly IEnumerable<INetworkServer> _servers;
		private readonly Dictionary<string, INetworkServer> _runningServers = new();

		public NetworkServerManager(IEnumerable<INetworkServer> servers)
		{
			_servers = servers;
		}

		// при передаче сюда названия протокола,
		// выбирается соответствующий по названию из реализаций INetworkServer и таким образом запускается.
		// один из минусов решения, что хардкодятся названия протоколов при такой реализации.
		public async Task StartServerAsync(string protocol, CancellationToken cancellationToken)
		{
			if (_runningServers.ContainsKey(protocol)) return;

			var server = _servers.FirstOrDefault(s => s.Protocol.Equals(protocol, StringComparison.OrdinalIgnoreCase));
			if (server == null) throw new InvalidOperationException($"Сервер с протоколом {protocol} не найден.");

			await server.StartAsync(cancellationToken);
			_runningServers[protocol] = server;
		}

		public async Task StopServerAsync(string protocol, CancellationToken cancellationToken)
		{
			if (_runningServers.TryGetValue(protocol, out var server))
			{
				await server.StopAsync(cancellationToken);
				_runningServers.Remove(protocol);
			}
		}

		public IReadOnlyCollection<string> GetRunningServers() => _runningServers.Keys.ToList();
	}
}
