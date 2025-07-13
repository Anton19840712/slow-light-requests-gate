using System.Text.Json.Serialization;

namespace lazy_light_requests_gate.core.domain.settings.common
{
	public record class ServerSettings : BaseConnectionSettings
	{
		[JsonPropertyName("clientHoldConnectionMs")]
		public int ClientHoldConnectionMs { get; set; }
	}
}
