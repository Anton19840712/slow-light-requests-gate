using System.Text.Json;

namespace lazy_light_requests_gate.core.application.interfaces.messaging
{
	public interface IMessageFormatter
	{
		string DecodeUnicodeEscape(string input);
		string FormatJson(string json);
		void WriteFormattedJson(JsonElement element, Utf8JsonWriter writer);
	}
}
