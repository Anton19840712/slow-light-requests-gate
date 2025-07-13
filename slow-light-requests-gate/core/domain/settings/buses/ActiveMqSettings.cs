using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.core.domain.settings.buses
{
	public class ActiveMqSettings : MessageBusBaseSettings
	{
		[Required]
		public string BrokerUri { get; set; } = string.Empty;
	}
}
