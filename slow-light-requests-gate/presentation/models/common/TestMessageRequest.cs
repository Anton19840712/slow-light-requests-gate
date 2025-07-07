namespace lazy_light_requests_gate.presentation.models.common
{
	public class TestMessageRequest
	{
		public string QueueName { get; set; }
		public string RoutingKey { get; set; }
		public string Message { get; set; }
	}
}
