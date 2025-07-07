using System.Text.Json.Serialization;

namespace domain.models.dynamicgatesettings.incomingjson
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
