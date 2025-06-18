namespace lazy_light_requests_gate.temp;

public interface IMessageBusService // аналог IMessageDataBaseService для переключения между базами данных
{
	string TransportName { get; }
	Task<string> StartAsync(MessageBusBaseSettings config, CancellationToken cancellationToken);
	Task StopAsync(CancellationToken cancellationToken);
	Task PublishMessageAsync(string topic, string key, string message);
	Task<string> WaitForResponseAsync(string topic, int timeoutMilliseconds = 15000, CancellationToken cancellationToken = default);
}
