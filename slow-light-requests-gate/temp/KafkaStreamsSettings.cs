namespace lazy_light_requests_gate.temp
{
	public class KafkaStreamsSettings : MessageBusBaseSettings
	{
		public string BootstrapServers { get; set; } = "";
		public string ApplicationId { get; set; } = "";
		public string InputTopic { get; set; } = "";
		public string OutputTopic { get; set; } = "";
	}
}
