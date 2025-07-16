namespace lazy_light_requests_gate.presentation.models.common
{
	public class DatabaseSwitchRequest
	{
		public string DatabaseType { get; set; }

		public bool InitializeSchema { get; set; } = true;
	}
}
