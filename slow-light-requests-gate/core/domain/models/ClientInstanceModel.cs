﻿using lazy_light_requests_gate.core.domain.settings.common;
using lazy_light_requests_gate.core.domain.valueobjects;

namespace lazy_light_requests_gate.core.domain.models
{
	/// <summary>
	/// Модель для клиента
	/// </summary>
	public class ClientInstanceModel : InstanceModel
	{
		public string ClientHost { get; set; }
		public int ClientPort { get; set; }
		public ClientSettings ClientConnectionSettings { get; set; }
		public ConnectionEndpoint ServerHostPort { get; set; }
	}
}
