namespace lazy_light_requests_gate.presentation.models.common
{
	public class DatabaseHealthStatus
	{
		public bool IsHealthy { get; set; }
		public string DatabaseType { get; set; }
		public string Message { get; set; }
		public DateTime LastChecked { get; set; }
	}
}
