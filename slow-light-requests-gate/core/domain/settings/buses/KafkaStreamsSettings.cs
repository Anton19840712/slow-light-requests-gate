using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.core.domain.settings.buses
{
	public class KafkaStreamsSettings : MessageBusBaseSettings
	{
		[Required]
		public string BootstrapServers { get; set; } = "localhost:9092";

		[Required]
		public string ApplicationId { get; set; } = "";

		public string ClientId { get; set; } = "";

		public string GroupId { get; set; } = "";

		public string AutoOffsetReset { get; set; } = "earliest";

		public bool EnableAutoCommit { get; set; } = true;

		public int SessionTimeoutMs { get; set; } = 30000;

		public string SecurityProtocol { get; set; } = "PLAINTEXT";
	}
}
