using System.Data;

namespace lazy_light_requests_gate.core.application.interfaces.common
{
	/// <summary>
	/// Фабрика для создания подключений к PostgreSQL
	/// </summary>
	public interface IPostgresConnectionFactory
	{
		string GetConnectionString();
		IDbConnection CreateConnection();
	}
}
