namespace lazy_light_requests_gate.temp
{
	public interface IMessageBusFactory
	{
		IMessageBusService Create(MessageBusBaseSettings config);
	}
}
