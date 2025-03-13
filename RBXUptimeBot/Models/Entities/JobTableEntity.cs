using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace RBXUptimeBot.Models.Entities
{
	[Table("JobTable", Schema = "public")]
	public class JobTableEntity
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		[Column("ID")]
		public int ID { get; set; }

		[Required]
		[Column("PlaceID")]
		public string PlaceID { get; set; } = default!;

		[Required]
		[Column("AccountCount")]
		public long AccountCount { get; set; }

		[Column("StartTime")]
		public DateTime? StartTime { get; set; }

		[Column("EndTime")]
		public DateTime? EndTime { get; set; }

		[Column("Description")]
		public string? Description { get; set; }
	}
}
