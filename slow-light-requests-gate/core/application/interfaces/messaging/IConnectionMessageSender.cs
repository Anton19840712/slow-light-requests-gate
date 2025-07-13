namespace lazy_light_requests_gate.core.application.interfaces.messaging
{
	public interface IConnectionMessageSender
	{
		Task SendMessageAsync(string queueForListening, CancellationToken cancellationToken);
	}
}
