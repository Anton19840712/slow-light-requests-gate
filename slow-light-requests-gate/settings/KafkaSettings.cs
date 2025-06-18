namespace lazy_light_requests_gate.settings
{
	public class KafkaSettings
	{
		public string SecurityProtocol { get; set; } = "Plaintext";
		public string SaslMechanism { get; set; }
		public string SaslUsername { get; set; }
		public string SaslPassword { get; set; }
		public int SessionTimeoutMs { get; set; } = 6000;
		public bool EnableAutoCommit { get; set; } = true;
		public string BootstrapServers { get; set; } = "localhost:9092";
		public string GroupId { get; set; } = "requests-gate-group";
		public string Topic { get; set; } = "requests-topic";
		public int AutoCommitIntervalMs { get; set; } = 1000;

		public string GetConnectionString()
		{
			return BootstrapServers;
		}
	}
}
