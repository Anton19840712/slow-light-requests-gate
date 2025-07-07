using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.presentation.models.settings.databases
{
	public class MongoDbSettings
	{
		[Required]
		public string ConnectionString { get; set; } = "";

		[Required]
		public string DatabaseName { get; set; } = "";

		public string User { get; set; } = "";

		public string Password { get; set; } = "";

		[Required]
		public MongoDbCollectionSettings Collections { get; set; } = new();
	}
}
