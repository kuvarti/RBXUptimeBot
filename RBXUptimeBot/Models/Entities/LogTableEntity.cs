using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace RBXUptimeBot.Models.Entities
{
	[Table("LogTable", Schema = "public")]
	public class LogTableEntity : Entity
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		[Column("ID")]			public int ID { get; set; }
		[Column("Level")]		public int Level { get; set; } = default!;
		[Column("Message")]		public string Message { get; set; }
		[Column("Timestamp")]	public DateTime? Timestamp { get; set; }
		[Column("Exception")]	public JsonDocument? Exception { get; set; }
	}
}
