using domain.models.dynamicgatesettings.incomingjson;

namespace domain.models.dynamicgatesettings.internalusage
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
