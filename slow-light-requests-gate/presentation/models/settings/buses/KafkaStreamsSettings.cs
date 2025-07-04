﻿using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.presentation.models.settings.buses
{
	public class KafkaStreamsSettings : MessageBusBaseSettings
	{
		[Required]
		public string BootstrapServers { get; set; } = "localhost:9092";

		[Required]
		public string ApplicationId { get; set; } = "";

		public string ClientId { get; set; } = "";

		[Required]
		public string InputTopic { get; set; } = "";

		[Required]
		public string OutputTopic { get; set; } = "";

		public string GroupId { get; set; } = "";

		public string AutoOffsetReset { get; set; } = "earliest";

		public bool EnableAutoCommit { get; set; } = true;

		public int SessionTimeoutMs { get; set; } = 30000;

		public string SecurityProtocol { get; set; } = "PLAINTEXT";
	}
}
