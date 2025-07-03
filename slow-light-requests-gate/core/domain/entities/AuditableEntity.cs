using System.ComponentModel.DataAnnotations.Schema;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace lazy_light_requests_gate.core.domain.entities
{
	public abstract class AuditableEntity
	{
		[BsonId]
		public Guid Id { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? UpdatedAtUtc { get; set; }
		public DateTime? DeletedAtUtc { get; set; }

		public string CreatedBy { get; set; }
		public string UpdatedBy { get; set; }
		public string DeletedBy { get; set; }

		public bool IsDeleted { get; set; } = false;

		public int Version { get; set; } = 1;

		public string IpAddress { get; set; }
		public string UserAgent { get; set; }
		public string CorrelationId { get; set; }
		public string ModelType { get; set; }
		public bool IsProcessed { get; set; }

		[BsonElement("createdAtFormatted")]
		[BsonIgnoreIfNull]
		public string CreatedAtFormatted { get; set; }

		[BsonIgnore]
		[NotMapped]
		public string FormattedDate => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

		public AuditableEntity()
		{
			CreatedAtFormatted = FormattedDate;
		}
	}
}
