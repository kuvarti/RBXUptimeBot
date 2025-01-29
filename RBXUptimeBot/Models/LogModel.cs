using static RBXUptimeBot.Classes.Logger;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RBXUptimeBot.Models
{
	public enum LogLevel
	{
		Trace,
		Debug,
		Information,
		Warning,
		Error,
		Critical
	}

	public class SerializableException
	{
		public string Message { get; set; }
		public string StackTrace { get; set; }
		public string Source { get; set; }
		public string TargetSite { get; set; }
		public string ExceptionType { get; set; }
		public SerializableException InnerException { get; set; }
		public Dictionary<string, object> Data { get; set; }
	}

	public class LogEntry: IEntity
	{
		[BsonId]
		[BsonRepresentation(BsonType.ObjectId)]
		public string? Id { get; set; }
		[BsonElement("Time")]			public DateTime Timestamp { get; set; }
		[BsonElement("Level")]			public LogLevel Level { get; set; }
		[BsonElement("Message")]		public string Message { get; set; }
		[BsonElement("Description")]	public SerializableException? Exception { get; set; }
	}
}
