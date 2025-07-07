using System.Text.Json.Serialization;

namespace domain.models.dynamicgatesettings.incomingjson
{
	public record class ConnectionSettings
	{
		[JsonPropertyName("clientSettings")]
		public ClientSettings ClientConnectionSettings { get; set; }

		[JsonPropertyName("serverSettings")]
		public ServerSettings ServerConnectionSettings { get; set; }
	}
}
