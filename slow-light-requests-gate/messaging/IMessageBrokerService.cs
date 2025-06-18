namespace lazy_light_requests_gate.messaging
{
	public interface IMessageBrokerService
	{
		Task PublishMessageAsync(string topicOrQueue, string routingKey, string message);
		Task<string> WaitForResponseAsync(string topicOrQueue, int timeoutMilliseconds = 15000);
		string GetBrokerType();
	}
}
