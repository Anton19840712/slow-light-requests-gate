namespace lazy_light_requests_gate.settings
{
	public class PostgresDbSettings
	{
		public string Host { get; set; } = "localhost";
		public int Port { get; set; } = 5432;
		public string Username { get; set; } = "postgres";
		public string Password { get; set; } = "";
		public string Database { get; set; } = "GatewayDB";

		public string GetConnectionString()
		{
			return $"Host={Host};Port={Port};Username={Username};Password={Password};Database={Database}";
		}
	}
}
