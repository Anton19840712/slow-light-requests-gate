namespace lazy_light_requests_gate.presentation.models.common
{
	public class DatabaseReconnectRequest
	{
		public string DatabaseType { get; set; }
		public Dictionary<string, object> ConnectionParameters { get; set; }
		public bool InitializeSchema { get; set; } = false;
	}
}
