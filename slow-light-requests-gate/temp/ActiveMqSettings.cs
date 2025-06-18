namespace lazy_light_requests_gate.temp
{
	public class ActiveMqSettings : MessageBusBaseSettings
	{
		public string BrokerUri { get; set; } = string.Empty;
		public string QueueName { get; set; } = string.Empty;
	}
}
