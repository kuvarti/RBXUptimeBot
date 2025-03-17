using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RBXUptimeBot.Models.Entities
{
	[Table("ProxyTable", Schema = "public")]
	public class ProxyTableEntity : Entity
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		[Column("ID")]
		public int ID { get; set; }
		[Required]
		[Column("ProxyIP")]
		public string ProxyIP { get; set; } = default!;

		[Required]
		[Column("ProxyPort")]
		public string ProxyPort { get; set; } = default!;

		[Required]
		[Column("ProxyUser")]
		public string ProxyUser { get; set; } = default!;

		[Required]
		[Column("ProxyPassword")]
		public string ProxyPassword { get; set; } = default!;

		[Column("ProxyName")]
		public string ProxyName { get; set; } = default!;
	}
}
