using RBXUptimeBot.Models.Entities;

namespace RBXUptimeBot.Classes
{
	public partial class Account
	{
		private void UpdateEntity()
		{
			Entity.LastUpdate = DateTime.UtcNow;
			try { AccountManager.AccountService.SaveChangesAsync(); } catch { }
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
