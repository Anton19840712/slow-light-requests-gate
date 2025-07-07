using System.Text.Json.Serialization;
using lazy_light_requests_gate.temp.models;

namespace lazy_light_requests_gate.core.domain.settings.common
{
	public record class DataOptions
	{
		[JsonPropertyName("client")]
		public bool IsClient { get; set; }

		[JsonPropertyName("server")]
		public bool IsServer { get; set; }

		[JsonPropertyName("serverDetails")]
		public ConnectionEndpoint ServerDetails { get; set; }

		[JsonPropertyName("clientDetails")]
		public ConnectionEndpoint ClientDetails { get; set; }
	}
}
