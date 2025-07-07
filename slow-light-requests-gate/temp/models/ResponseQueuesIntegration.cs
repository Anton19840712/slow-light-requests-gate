namespace lazy_light_requests_gate.temp.models
{
	/// <summary>
	/// Частная модель для работы с возвратом информации из сервиса по созданию очередей.
	/// </summary>
	public class ResponseQueuesIntegration : ResponseIntegration
	{
		public string OutQueue { get; set; }
	}
}
