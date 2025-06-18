namespace lazy_light_requests_gate.messaging
{
	public interface IMessageBrokerFactory
	{
		IMessageBrokerService CreateMessageBroker(string brokerType);
		void SetDefaultBrokerType(string brokerType);
		string GetCurrentBrokerType();
	}
}
