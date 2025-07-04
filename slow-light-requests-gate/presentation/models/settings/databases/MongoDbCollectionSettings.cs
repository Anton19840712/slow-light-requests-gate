﻿using System.ComponentModel.DataAnnotations;

namespace lazy_light_requests_gate.presentation.models.settings.databases
{
	public class MongoDbCollectionSettings
	{
		[Required]
		public string QueueCollection { get; set; } = "QueueEntities";

		[Required]
		public string OutboxCollection { get; set; } = "OutboxMessages";

		[Required]
		public string IncidentCollection { get; set; } = "IncidentEntities";
	}
}
