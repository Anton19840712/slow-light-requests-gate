using System.Text.Json;

namespace lazy_light_requests_gate.temp
{
	public interface IUnifiedMessageBusManager
	{
		IEnumerable<string> GetRunningBusIds();
		IEnumerable<BusInformationDto> GetRunningBusInfo();
		Task StartBusAsync(MessageBusBaseSettings config, CancellationToken cancellationToken);
		Task StartFromConfigFileAsync(CancellationToken cancellationToken);
		Task StartViaRestRequetAsync(JsonDocument json, CancellationToken cancellationToken);
		Task StopAllBusesAsync(CancellationToken cancellationToken);
		Task StopBusAsync(string id, CancellationToken cancellationToken);
	}
}
