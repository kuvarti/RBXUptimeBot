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
			modelBuilder.Ignore<SerializableException>();
			base.OnModelCreating(modelBuilder);
		}

		public DbSet<T> Table { get; set; }
	}
}
