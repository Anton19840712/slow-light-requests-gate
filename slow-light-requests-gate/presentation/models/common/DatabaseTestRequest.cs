namespace lazy_light_requests_gate.presentation.models.common
{
	public class DatabaseTestRequest
	{
		public string DatabaseType { get; set; }
		public Dictionary<string, object> ConnectionParameters { get; set; }
	}
}
