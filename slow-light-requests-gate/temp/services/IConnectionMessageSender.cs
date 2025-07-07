namespace application.interfaces.services
{
	public interface IConnectionMessageSender
	{
		Task SendMessageAsync(string queueForListening, CancellationToken cancellationToken);
	}
}
