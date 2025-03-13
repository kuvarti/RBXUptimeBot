using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace RBXUptimeBot.Models.Entities
{
	[Table("AccountTable", Schema = "public")]
	public class AccountTableEntity
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		[Column("ID")]
		public int ID { get; set; }

		[Required]
		[Column("Username")]
		public string Username { get; set; } = default!;

		[Required]
		[Column("Password")]
		public string Password { get; set; } = default!;

		[Column("Mail")]
		public string? Mail { get; set; }

		[Column("Token")]
		public string? Token { get; set; }

		[Column("TokenCreatedTime")]
		public DateTime? TokenCreatedTime { get; set; }

		[Column("State")]
		public string? State { get; set; }

		[Column("Status")]
		public string? Status { get; set; }

		// Foreign key sütunu: ProxyTable tablosuna referans verir.
		[Required]
		[Column("Proxy")]
		public int Proxy { get; set; }

		// Navigation property (isteğe bağlı)
		[ForeignKey("Proxy")]
		public ProxyTableEntity? ProxyTable { get; set; }
	}
}
