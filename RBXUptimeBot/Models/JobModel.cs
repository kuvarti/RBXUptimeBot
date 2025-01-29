using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RBXUptimeBot.Models
{
	public class JobEntry : IEntity
	{
		[BsonId]
		[BsonRepresentation(BsonType.ObjectId)] public string? Id { get; set; }
		[BsonElement("PlaceId")]		public string placeId {get; set;}
		[BsonElement("AccountCount")]	public int AccCount { get; set; }
		[BsonElement("StartTime")]		public DateTime? StartTime {get; set;}
		[BsonElement("EndTime")]		public DateTime? EndTime {get; set;}
		[BsonElement("Description")]	public string Description { get; set;}
	}
}
