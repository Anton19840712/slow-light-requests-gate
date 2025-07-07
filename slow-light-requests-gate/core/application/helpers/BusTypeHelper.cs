namespace lazy_light_requests_gate.core.application.helpers
{
	/// <summary>
	/// Хелпер для нормализации типов шин сообщений
	/// </summary>
	public static class BusTypeHelper
	{
		public static string Normalize(string busType)
		{
			return busType?.ToLowerInvariant() switch
			{
				"rabbitmq" => "rabbit",
				"rabbit" => "rabbit",
				"activemq" => "activemq",
				"apache-pulsar" => "pulsar",
				"pulsar" => "pulsar",
				"kafka" => "kafkastreams",
				"kafkastreams" => "kafkastreams",
				"kafka-streams" => "kafkastreams",
				"tarantool" => "tarantool",
				_ => busType?.ToLowerInvariant()
			};
		}

		public static bool IsValidBusType(string busType)
		{
			var normalized = Normalize(busType);
			return normalized is "rabbit" or "activemq" or "pulsar" or "kafkastreams" or "tarantool";
		}
	}
}
