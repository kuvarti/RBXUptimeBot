

namespace RBXUptimeBot.Classes
{
	public class JobManager
	{
		public static async Task<string> JobStarter(long jId, int accCount, DateTime end)
		{
			if (AccountManager.AccountsList.Count - AccountManager.AllRunningAccounts.Count < accCount)
				return $"Not enough accounts to start the job.({AccountManager.AccountsList.Count})";
			AccountManager.ActiveJobs.Add(new ActiveJob()
			{
				Jid = jId,
				AccountCount = accCount,
				startTime = DateTime.Now,
				endTime = end,
				ProcessList = new List<ActiveItem>()
			});
			new Thread(async () => await JobController(jId)).Start();
			Logger.Trace($"job {jId} started in {DateTime.Now}");
			return $"Job {jId} started.";
		}

		public static async Task JobFinisher(long jid)
		{
			ActiveJob job = AccountManager.ActiveJobs.Find(x => x.Jid == jid);
			if (job == null)
			{
				Logger.Error($"JobFinisher-Job {jid} cannot found.");
				return;
			}
			foreach (var item in job.ProcessList)
			{
				item.Account.LeaveServer();
				AccountManager.AllRunningAccounts.RemoveAll(x => x.PID == item.PID);
			}
			AccountManager.ActiveJobs.Remove(job);
			Logger.Information($"Job {jid} is finished {DateTime.Now}.");
			// TODO: Add a log entry for the job finish to the db or json file
		}

		public static async Task JobController(long jid)
		{
			ActiveJob job = AccountManager.ActiveJobs.Find(x => x.Jid == jid);
			if (job == null) return;
			job.isRunning = true;
			while (DateTime.Now < job.endTime)
			{
				foreach (var item in job.ProcessList)
				{
					if (item.StartTime.AddMinutes(20) < DateTime.Now) //TODO THIS IS ORIGINALLY 20
						RestartProcess(jid, item);
				}
				await Task.Delay(5000);
				if (job.AccountCount > job.ProcessList.Count) {
					await AddProcess(jid);
				}
				if (!job.isRunning)
					break;
			}
			JobFinisher(jid);
		}

		// Assingning early bc; it can be used from another process while running
		public static async Task AddProcess(long jid)
		{
			List<Account> items = new List<Account>();
			ActiveJob job = AccountManager.ActiveJobs.Find(x => x.Jid == jid);

			if (job == null) return;
			foreach (var account in AccountManager.AccountsList.FindAll(a => a.IsActive == 0))
			{
				if (job.ProcessList.Count + items.Count >= job.AccountCount)
				{
					break;
				}
				account.IsActive = 1;
				items.Add(account);
			}
			if (items.Count == 0)
				Logger.Warning($"No more accounts to launch for job {jid}");
			await AccountManager.LaunchAccounts(items, jid, "", new CancellationTokenSource());
		}

		public static async Task RestartProcess(long jid, ActiveItem a) {
			if (a.Account.IsActive == 0) return;
			a.Account.LeaveServer();
			await Task.Delay(60000);
			a.Account.JoinServer(jid);
		}
	}
}
