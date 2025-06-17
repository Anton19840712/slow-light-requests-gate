namespace lazy_light_requests_gate.configurationsettings
{
	public class MongoDbSettings
	{
		public string ConnectionString { get; set; }
		public string DatabaseName { get; set; }
		public MongoDbCollections Collections { get; set; }
	}

	public class MongoDbCollections
	{
		public string QueueCollection { get; set; }
		public string OutboxCollection { get; set; }
		public string IncidentCollection { get; set; }
	}
}