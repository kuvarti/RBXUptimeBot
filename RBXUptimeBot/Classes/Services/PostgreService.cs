using Microsoft.EntityFrameworkCore;
using RBXUptimeBot.Models;
using RBXUptimeBot.Models.Entities;

namespace RBXUptimeBot.Classes.Services
{
	public class PostgreService<T> : DbContext where T : Entity
	{
		public PostgreService(DbContextOptions<PostgreService<T>> options)
			: base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
		}

		public override int SaveChanges() {
			try {
				return base.SaveChanges();
			} catch (Exception ex){
				Logger.Log(Models.LogLevel.Error, $"", ex, false);
			}
			return 0;
		}

		public DbSet<T> Table { get; set; }
	}
}
