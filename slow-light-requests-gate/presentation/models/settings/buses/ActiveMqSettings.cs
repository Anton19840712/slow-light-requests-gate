using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.presentation.models.settings.buses
{
	public class ActiveMqSettings : MessageBusBaseSettings
	{
		[Required]
		public string BrokerUri { get; set; } = string.Empty;

		[Required]
		public string PushQueueName { get; set; } = string.Empty;

		[Required]
		public string ListenQueueName { get; set; } = string.Empty;
	}
}
