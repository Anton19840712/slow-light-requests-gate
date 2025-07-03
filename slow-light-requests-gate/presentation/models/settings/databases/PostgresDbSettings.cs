using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.presentation.models.settings.databases
{
	public class PostgresDbSettings
	{
		[Required]
		public string Host { get; set; } = "localhost";

		[Range(1, 65535)]
		public int Port { get; set; } = 5432;

		[Required]
		public string Username { get; set; } = "postgres";

		[Required]
		public string Password { get; set; } = "";

		[Required]
		public string Database { get; set; } = "GatewayDB";

		public string GetConnectionString()
		{
			return $"Host={Host};Port={Port};Username={Username};Password={Password};Database={Database}";
		}
	}
}
