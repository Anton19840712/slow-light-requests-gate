namespace lazy_light_requests_gate.presentation.models.request
{
	public class BusReconnectRequest
	{
		public string BusType { get; set; }
		public Dictionary<string, object> ConnectionParameters { get; set; }
	}
}
