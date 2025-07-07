namespace lazy_light_requests_gate.temp.models
{
	/// <summary>
	/// Модель для сервера
	/// </summary>
	public class ServerInstanceModel : InstanceModel
	{
		public string Host { get; set; }
		public int Port { get; set; }
		public ServerSettings ServerConnectionSettings { get; set; }
	}
}
