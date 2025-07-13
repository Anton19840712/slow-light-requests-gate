using System.ComponentModel.DataAnnotations;
using lazy_light_requests_gate.core.domain.settings.buses;
using lazy_light_requests_gate.core.domain.settings.buses.lazy_light_requests_gate.core.domain.settings.buses;
using lazy_light_requests_gate.core.domain.settings.databases;

namespace lazy_light_requests_gate.core.domain.settings.common
{
	/// <summary>
	/// Модель конфигурации шлюза с валидацией
	/// </summary>
	public class GateConfigurationSettings
	{
		[Required]
		public string Type { get; set; } = "rest";

		[Required]
		public string CompanyName { get; set; } = "";

		[Required]
		public string Host { get; set; } = "localhost";

		[Range(1, 65535)]
		public int Port { get; set; } = 5000;

		[Range(1, 65535)]
		public int PortHttp { get; set; } = 80;

		[Range(1, 65535)]
		public int PortHttps { get; set; } = 443;

		public bool Validate { get; set; } = true;

		[Required]
		[RegularExpression("^(postgres|mongo)$", ErrorMessage = "Database должен быть 'postgres' или 'mongo'")]
		public string Database { get; set; } = "mongo";

		[Required]
		[RegularExpression("^(rabbit|activemq|kafka|pulsar|tarantool)$", ErrorMessage = "Bus должен быть 'rabbit', 'activemq', 'kafka', 'pulsar' или 'tarantool'")]
		public string Bus { get; set; } = "rabbit";

		[Required]
		[RegularExpression("^(tcp|udp|websockets)$", ErrorMessage = "Protocol должен быть 'tcp', 'udp' или 'websockets'")]
		public string Protocol { get; set; } = "tcp";

		[Required]
		[RegularExpression("^(json|xml|binary|protobuf)$", ErrorMessage = "DataFormat должен быть 'json', 'xml', 'binary' или 'protobuf'")]
		public string DataFormat { get; set; } = "json";

		[Range(1, 3600)]
		public int CleanupIntervalSeconds { get; set; } = 5;

		[Range(1, 86400)]
		public int OutboxMessageTtlSeconds { get; set; } = 10;

		[Range(0, 120)]
		public int IncidentEntitiesTtlMonths { get; set; } = 60;

		public PostgresDbSettings?PostgresDbSettings { get; set; }
		public MongoDbSettings MongoDbSettings { get; set; }
		public RabbitMqSettings RabbitMqSettings { get; set; }
		public ActiveMqSettings ActiveMqSettings { get; set; }
		public KafkaStreamsSettings KafkaStreamsSettings { get; set; }
		public PulsarSettings PulsarSettings { get; set; }
		public TarantoolSettings TarantoolSettings { get; set; }
		public DataOptions DataOptions { get; set; }
	}
}
