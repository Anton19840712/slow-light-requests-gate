using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.core.domain.settings.common
{
	/// <summary>
	/// Модель конфигурации для stream-режима
	/// </summary>
	public class StreamConfigurationSettings
	{
		[Required]
		public string Type { get; set; } = "stream";

		[Required]
		[RegularExpression("^(TCP|UDP|WS)$", ErrorMessage = "Protocol должен быть 'TCP', 'UDP' или 'WS'")]
		public string Protocol { get; set; } = "TCP";

		[Required]
		[RegularExpression("^(json|xml|binary)$", ErrorMessage = "DataFormat должен быть 'json', 'xml' или 'binary'")]
		public string DataFormat { get; set; } = "json";

		[Required]
		public DataOptions DataOptions { get; set; }
	}
}
