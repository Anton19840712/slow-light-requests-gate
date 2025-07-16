using lazy_light_requests_gate.presentation.models.common;

namespace lazy_light_requests_gate.core.application.interfaces.databases
{
	public interface IDynamicDatabaseManager
	{
		Task SwitchDatabaseAsync(string databaseType);
		Task ReconnectWithNewParametersAsync(string databaseType, Dictionary<string, object> parameters);
		Task<ConnectionTestResult> TestConnectionAsync(string databaseType, Dictionary<string, object> parameters);
		Task<object> GetCurrentConnectionInfoAsync();
		Task<DatabaseHealthStatus> CheckHealthAsync();
		string GetCurrentDatabaseType();
		Task InitializeDatabaseSchemaAsync(string dbType);
	}
}
