using System.Data;

namespace lazy_light_requests_gate.core.application.interfaces.databases
{
	public interface IDynamicPostgresClient
	{
		IDbConnection GetConnection();
		string GetConnectionString();
		Task ReconnectAsync(Dictionary<string, object> parameters);
	}
}
