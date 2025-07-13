using lazy_light_requests_gate.core.application.interfaces.messaging;
using lazy_light_requests_gate.core.application.interfaces.networking;
using lazy_light_requests_gate.core.application.services.messaging;

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
