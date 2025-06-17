namespace lazy_light_requests_gate.configurationsettings
{
	public class RabbitMqSettings
	{
		public string HostName { get; set; }
		public int Port { get; set; }
		public string UserName { get; set; }
		public string Password { get; set; }
		public string VirtualHost { get; set; }
		public Uri GetAmqpUri()
		{
			// VirtualHost может быть null или пустым, тогда используем /
			var vhost = string.IsNullOrWhiteSpace(VirtualHost) ? "/" : VirtualHost.TrimStart('/');

			var uriString = $"amqp://{UserName}:{Password}@{HostName}/{vhost}";
			return new Uri(uriString);
		}
	}
}
