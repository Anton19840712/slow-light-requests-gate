using MongoDB.Driver;

namespace lazy_light_requests_gate.core.application.interfaces.databases
{
	public interface IDynamicMongoClient : IDisposable
	{
		IMongoClient GetClient();
		IMongoDatabase GetDatabase();
		IMongoDatabase GetDatabase(string databaseName);
		Task ReconnectAsync(Dictionary<string, object> parameters);
	}
}
