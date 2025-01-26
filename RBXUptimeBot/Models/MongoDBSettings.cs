using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace RBXUptimeBot.Models
{
	public class MongoDBSettings
	{
		public string ConnectionString { get; set; } = null!;
		public string DatabaseName { get; set; } = null!;
		public Dictionary<string, string> Collections { get; set; } = new();
	}

	public interface IEntity
	{
		[BsonId]
		[BsonRepresentation(BsonType.ObjectId)]
		string? Id { get; set; }
	}
}
