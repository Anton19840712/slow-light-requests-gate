using lazy_light_requests_gate.temp;

namespace lazy_light_requests_gate.settings
{
	public class RabbitMqSettings : MessageBusBaseSettings
	{
		public string HostName { get; set; }
		public int Port { get; set; }
		public string UserName { get; set; }
		public string Password { get; set; }
		public string VirtualHost { get; set; }
		public string Heartbeat { get; set; }
		public string PushQueueName { get; set; }
		public string ListenQueueName { get; set; }
		public Uri GetAmqpUri()
		{
			// VirtualHost может быть null или пустым, тогда используем /
			var vhost = string.IsNullOrWhiteSpace(VirtualHost) ? "/" : VirtualHost.TrimStart('/');

			var uriString = $"amqp://{UserName}:{Password}@{HostName}/{vhost}";
			return new Uri(uriString);
		}
	}
}
