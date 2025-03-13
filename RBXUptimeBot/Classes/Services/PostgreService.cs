using Microsoft.EntityFrameworkCore;
using RBXUptimeBot.Models;
using RBXUptimeBot.Models.Entities;

namespace RBXUptimeBot.Classes.Services
{	public class PostgreService : DbContext
	{
		public PostgreService(DbContextOptions<PostgreService> options)
			: base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Ignore<SerializableException>();
			//modelBuilder.Entity<ProxyTableEntity>().HasKey(e => e.ID);
			modelBuilder.Entity<LogTableEntity>().HasNoKey();
			base.OnModelCreating(modelBuilder);
			// Ek konfigürasyonlar (ör. Fluent API ile özel eşlemeler) burada yapılabilir.
		}

		public DbSet<ProxyTableEntity> ProxyTable { get; set; }
		public DbSet<AccountTableEntity> AccountTable { get; set; }
		public DbSet<JobTableEntity> JobTable { get; set; }
		public DbSet<LogTableEntity> LogTable { get; set; }
	}
}
