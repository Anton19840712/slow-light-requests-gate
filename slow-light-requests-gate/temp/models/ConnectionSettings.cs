using System.Text.Json.Serialization;

namespace lazy_light_requests_gate.temp.models
{
	public record class ConnectionSettings
	{
		[JsonPropertyName("clientSettings")]
		public ClientSettings ClientConnectionSettings { get; set; }

		[JsonPropertyName("serverSettings")]
		public ServerSettings ServerConnectionSettings { get; set; }
	}
}
