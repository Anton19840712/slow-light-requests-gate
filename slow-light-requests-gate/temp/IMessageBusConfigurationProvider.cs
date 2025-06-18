using System.Text.Json;

namespace lazy_light_requests_gate.temp
{
	public interface IMessageBusConfigurationProvider
	{
		MessageBusBaseSettings GetConfiguration(JsonDocument jsonDoc=null);
		MessageBusBaseSettings ParseConfiguration(JsonElement json, string type);
	}
}
