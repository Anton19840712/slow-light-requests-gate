namespace lazy_light_requests_gate.presentation.models.common
{
	public class ConnectionTestResult
	{
		public bool IsSuccess { get; set; }
		public string Message { get; set; }
		public object ConnectionInfo { get; set; }
	}
}
