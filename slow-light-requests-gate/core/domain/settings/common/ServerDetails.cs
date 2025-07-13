using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.core.domain.settings.common
{
	/// <summary>
	/// Настройки сервера
	/// </summary>
	public class ServerDetails
	{
		[Required]
		public string Host { get; set; } = "127.0.0.1";

		[Range(1, 65535)]
		public int Port { get; set; } = 6254;
	}
}
