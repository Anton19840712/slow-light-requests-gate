namespace lazy_light_requests_gate.temp
{
	public abstract class MessageBusBaseSettings
	{
		public string InstanceNetworkGateId { get; set; } = string.Empty;
		public MessageBusType TypeToRun { get; set; }
	}
}
