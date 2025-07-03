namespace lazy_light_requests_gate.core.application.interfaces.buses
{
	/// <summary>
	/// Интерфейс для ActiveMQ (заглушка, реализуйте согласно вашей ActiveMQ библиотеке)
	/// </summary>
	public interface IActiveMqService : IMessageBusService
	{
		Task PublishMessageAsync(string queueName, string message);
	}
}
