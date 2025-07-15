namespace lazy_light_requests_gate.presentation.models.response
{
	/// <summary>
	/// Стандартизированная модель ответа API
	/// </summary>
	public class ApiResponse<T>
	{
		public bool Success { get; set; }
		public string Message { get; set; }
		public T Data { get; set; }
		public DateTime Timestamp { get; set; }
		public string RequestId { get; set; }
	}
}
