using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.core.domain.settings.databases
{
	public class MongoDbCollectionSettings
	{
		[Required]
		public string OutboxCollection { get; set; } = "OutboxMessages";

		[Required]
		public string IncidentCollection { get; set; } = "IncidentEntities";
	}
}
