using System.ComponentModel.DataAnnotations.Schema;
using lazy_light_requests_gate.models;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization.Attributes;

namespace lazy_light_requests_gate
{
	public class OutboxMessage
	{

		[BsonId]
		//[BsonRepresentation(BsonType.String)]
		public Guid Id { get; set; }

		[BsonElement("modelType")]
		public ModelType ModelType { get; set; }

		[BsonElement("eventType")]
		public EventTypes EventType { get; set; }

		[BsonElement("isProcessed")]
		public bool IsProcessed { get; set; }

		[BsonElement("processedAt")]
		public DateTime ProcessedAt { get; set; }

		[BsonElement("outQueue")]
		public string OutQueue { get; set; }

		[BsonElement("inQueue")]
		public string InQueue { get; set; }

		[BsonElement("payload")]
		public string Payload { get; set; }

		[BsonElement("routing_key")]
		public string RoutingKey { get; set; }

		[BsonElement("createdAt")]
		[BsonRepresentation(BsonType.DateTime)]
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		// Принудительно сохраняем в UTC
		[BsonElement("createdAtFormatted")]
		[BsonIgnoreIfNull]
		public string CreatedAtFormatted { get; set; }

		// Принудительно сохраняем в UTC
		[BsonElement("source")]
		[BsonIgnoreIfNull]
		public string Source { get; set; }

		[BsonIgnore]
		[NotMapped]
		public string FormattedDate => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

		public OutboxMessage()
		{
			CreatedAtFormatted = FormattedDate; // Заполняем перед сохранением
		}

		public string GetPayloadJson()
		{
			return Payload?.ToJson(new JsonWriterSettings()) ?? string.Empty;
		}
	}
}