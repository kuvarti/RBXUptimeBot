using Microsoft.EntityFrameworkCore;
using RBXUptimeBot.Classes.Services;
using RBXUptimeBot.Models.Entities;

namespace RBXUptimeBot.Classes
{
	public partial class Account
	{
		private void UpdateEntity()
		{
			Entity.LastUpdate = DateTime.UtcNow;
			using (var postgre = new PostgreService<AccountTableEntity>(new DbContextOptionsBuilder<PostgreService<AccountTableEntity>>().UseNpgsql(AccountManager.ConnStr).Options))
			{
				try {
					postgre.Attach(Entity);
					postgre.Entry(Entity).State = EntityState.Modified;
					postgre.SaveChanges();
				} catch (Exception ex) {
					Logger.Error($"Error while updating account entity: {ex.Message}", ex);
				}
			}
		}

		private void UpdateToken(string token)
		{
			Entity.Token = token;
			Entity.TokenCreatedTime = DateTime.UtcNow;
			UpdateEntity();
		}

		private void UpdateStatus(string status)
		{
			Entity.Status = status;
			UpdateEntity();
		}

		private void UpdateState(string state)
		{
			Entity.State = state;
			UpdateEntity();
		}
	}
}
