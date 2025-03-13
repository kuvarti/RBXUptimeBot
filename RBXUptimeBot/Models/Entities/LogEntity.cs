using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace RBXUptimeBot.Models.Entities
{
	[Table("LogTable", Schema = "public")]
	public class LogTableEntity
	{
		[Column("Level")]		public int Level { get; set; } = default!;
		[Column("Message")]		public string Message { get; set; }
		[Column("Timestamp")]	public DateTime? Timestamp { get; set; }
		[Column("Exception")]	public SerializableException? Exception { get; set; }
	}
}
