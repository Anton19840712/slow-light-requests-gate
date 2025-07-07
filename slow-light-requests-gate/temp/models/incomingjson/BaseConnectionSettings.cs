using System.Text.Json.Serialization;

namespace domain.models.dynamicgatesettings.incomingjson
{
	public record class BaseConnectionSettings
	{
		[JsonPropertyName("attemptsToFindBus")]
		public int AttemptsToFindBus { get; set; }

		[JsonPropertyName("busResponseWaitTimeMs")]
		public int BusResponseWaitTimeMs { get; set; }

		[JsonPropertyName("busProcessingTimeMs")]
		public int BusProcessingTimeMs { get; set; }

		[JsonPropertyName("busReconnectDelayMs")]
		public int BusReconnectDelayMs { get; set; }

		[JsonPropertyName("busIdleTimeoutMs")]
		public int BusIdleTimeoutMs { get; set; }
	}
}
