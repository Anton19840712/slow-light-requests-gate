using lazy_light_requests_gate.presentation.enums;

namespace lazy_light_requests_gate.core.domain.settings.common
{
	/// <summary>
	/// Настройки для режимов работы клиент/сервер
	/// </summary>
	public class DataOptions
	{
		public bool Client { get; set; } = false;
		public bool Server { get; set; } = true;
		public ServerDetails ServerDetails { get; set; } = new();
		public ClientDetails ClientDetails { get; set; } = new();
	}
}
