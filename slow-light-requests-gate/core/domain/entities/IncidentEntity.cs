using MongoDB.Bson.Serialization.Attributes;

namespace lazy_light_requests_gate.core.domain.entities
{
	[BsonIgnoreExtraElements]
	public class IncidentEntity : AuditableEntity
	{
		public string Payload { get; set; }
	}
}
