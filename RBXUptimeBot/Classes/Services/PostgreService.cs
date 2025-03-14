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
			modelBuilder.Entity<LogTableEntity>().HasNoKey();
			base.OnModelCreating(modelBuilder);
		}

		public DbSet<ProxyTableEntity> ProxyTable => Database.CanConnect() ? Set<ProxyTableEntity>() : null;
		public DbSet<AccountTableEntity> AccountTable { get; set; }
		public DbSet<JobTableEntity> JobTable { get; set; }
		public DbSet<LogTableEntity> LogTable { get; set; }
	}
}
