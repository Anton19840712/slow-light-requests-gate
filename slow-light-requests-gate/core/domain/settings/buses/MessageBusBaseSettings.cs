using System.ComponentModel.DataAnnotations;
using lazy_light_requests_gate.presentation.enums;

namespace lazy_light_requests_gate.core.domain.settings.buses
{
	public abstract class MessageBusBaseSettings
	{
		public string InstanceNetworkGateId { get; set; } = string.Empty;
		public MessageBusType TypeToRun { get; set; }

		[Required]
		public string InputChannel { get; set; } = string.Empty;

		[Required]
		public string OutputChannel { get; set; } = string.Empty;
	}
}
