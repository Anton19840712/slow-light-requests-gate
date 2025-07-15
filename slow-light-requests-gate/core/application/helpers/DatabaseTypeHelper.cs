namespace lazy_light_requests_gate.core.application.helpers
{
	public static class DatabaseTypeHelper
	{
		public static string Normalize(string databaseType)
		{
			return databaseType?.ToLowerInvariant() switch
			{
				"mongodb" => "mongo",
				"mongo" => "mongo",
				"postgresql" => "postgres",
				"postgres" => "postgres",
				_ => databaseType?.ToLowerInvariant()
			};
		}

		public static bool IsValidDatabaseType(string databaseType)
		{
			var normalized = Normalize(databaseType);
			return normalized == "mongo" || normalized == "postgres";
		}
	}
}
