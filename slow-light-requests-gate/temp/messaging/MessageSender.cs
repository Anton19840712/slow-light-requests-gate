using application.interfaces.networking;
using application.interfaces.services;
using infrastructure.messaging;

public class MessageSender : IMessageSender
{
	private readonly ConnectionMessageSenderFactory _senderFactory;

	public MessageSender(ConnectionMessageSenderFactory senderFactory)
	{
		_senderFactory = senderFactory;
	}

	public async Task SendMessagesToClientAsync(
		IConnectionContext connectionContext,
		string queueForListening,
		CancellationToken cancellationToken)
	{
		var sender = _senderFactory.CreateSender(connectionContext);
		await sender.SendMessageAsync(queueForListening, cancellationToken);
	}
}
