using lazy_light_requests_gate.presentation.enums;

namespace lazy_light_requests_gate.presentation.models.settings.buses
{
	public abstract class MessageBusBaseSettings
	{
		public string InstanceNetworkGateId { get; set; } = string.Empty;
		public MessageBusType TypeToRun { get; set; }
	}
}
