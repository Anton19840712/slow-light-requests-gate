namespace application.interfaces.networking
{
	public interface INetworkServer
	{
		string Protocol { get; }
		bool IsRunning { get; }
		Task StartAsync(CancellationToken cancellationToken);
		Task StopAsync(CancellationToken cancellationToken);
	}
}
