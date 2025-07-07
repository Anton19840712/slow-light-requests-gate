using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.core.domain.settings.buses
{
	public class RabbitMqSettings : MessageBusBaseSettings
	{
		[Required]
		public string HostName { get; set; }

		[Range(1, 65535)]
		public int Port { get; set; }

		[Required]
		public string UserName { get; set; }

		[Required]
		public string Password { get; set; }

		public string VirtualHost { get; set; }

		public string Heartbeat { get; set; }

		[Required]
		public string PushQueueName { get; set; }

		[Required]
		public string ListenQueueName { get; set; }

		public Uri GetAmqpUri()
		{
			// Нормализуем VirtualHost - ВСЕГДА должен начинаться с /
			var vhost = string.IsNullOrWhiteSpace(VirtualHost) ? "/" : VirtualHost;
			if (!vhost.StartsWith("/"))
				vhost = "/" + vhost;

			// ВАЖНО: Port тоже должен быть в URI
			var uriString = $"amqp://{UserName}:{Password}@{HostName}:{Port}{vhost}";
			return new Uri(uriString);
		}
	}
}
