using lazy_light_requests_gate.core.application.interfaces.common;
using Npgsql;
using System.Data;

namespace lazy_light_requests_gate.core.application.services.common
{
	/// <summary>
	/// Реализация фабрики подключений к PostgreSQL
	/// </summary>
	public class PostgresConnectionFactory : IPostgresConnectionFactory
	{
		private readonly string _connectionString;

		public PostgresConnectionFactory(string connectionString)
		{
			_connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
		}

		public string GetConnectionString() => _connectionString;

		public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
	}
}
