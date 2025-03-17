using Microsoft.Extensions.Options;
using PuppeteerExtraSharp;
using RBXUptimeBot.Models;
using RBXUptimeBot.Models.Entities;
using System.Diagnostics;
using System.Security.Cryptography;
using static System.Net.Mime.MediaTypeNames;

namespace RBXUptimeBot.Classes.Services
{
	public class JobService
	{
		public JobService()
		{ }

		public async Task<string> JobStarter(long jId, int accCount, DateTime end)
		{
			if (AccountManager.AccountsList.Count - AccountManager.AllRunningAccounts.Count < accCount)
				return $"Not enough accounts to start the job.({AccountManager.AccountsList.Count})";
			var jobEntity = new JobTableEntity() {
				PlaceID = jId.ToString(),
				AccountCount = accCount,
				StartTime = DateTime.UtcNow,
				EndTime = null,
				Description = null,
			};
			var job = AccountManager.postgreService.JobTable?.AddAsync(jobEntity); //test this
			await AccountManager.postgreService?.SaveChangesAsync();
			AccountManager.ActiveJobs.Add(new ActiveJob()
			{
				JobEntity = jobEntity,
				AccountCount = accCount,
				startTime = DateTime.Now,
				endTime = end,
				DBid = "0",
				ProcessList = new List<ActiveItem>()
			});
			new Thread(async () => await JobController(jId)).Start();

			Logger.Trace($"job {jId} started in {DateTime.Now}");
			return $"Job {jId} started.";
		}

		public async Task JobFinisher(long jid)
		{
			ActiveJob job = AccountManager.ActiveJobs.Find(x => x.JobEntity.PlaceID == jid.ToString());
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

			job.JobEntity.EndTime = DateTime.UtcNow;
			job.JobEntity.AccountCount = job.AccountCount;
			AccountManager.postgreService?.SaveChangesAsync();
			AccountManager.ActiveJobs.Remove(job);
			Logger.Trace($"job {jid} is finished {DateTime.Now}");
		}

		public async Task JobController(long jid)
		{
			ActiveJob job = AccountManager.ActiveJobs.Find(x => x.JobEntity.PlaceID == jid.ToString());
			List<Account> accounts = new List<Account>();
			if (job == null) return;
			job.isRunning = true;
			while (DateTime.Now < job.endTime)
			{
				foreach (var item in job.ProcessList)
				{
					try
					{//todo investigate item.PID goes 
						if (Process.GetProcessById(item.PID).MainWindowTitle != "Roblox")
						{
							Logger.Error($"Something is wrong with client {item.Account.Username}: Main Window title isnt 'Roblox'");
							accounts.Add(item.Account);
						}
					}
					catch (Exception ex) {
						Logger.Critical($"Acccount {item.Account.Username} pid goes blank unexpectedly");
					}
				}
				foreach (var account in accounts)
				{
					account.LeaveServer();
					job.ProcessList.RemoveAll(ritem => ritem.Account == account);
				}
				accounts.Clear();
				if (job.AccountCount > job.ProcessList.Count)
					await AddProcess(jid);
				await Task.Delay(5000);
				if (!job.isRunning)
					break;
			}
			JobFinisher(jid);
		}

		// Assingning early bc; it can be used from another process while running
		public async Task AddProcess(long jid)
		{
			List<Account> items = new List<Account>();
			ActiveJob job = AccountManager.ActiveJobs.Find(x => x.JobEntity.PlaceID == jid.ToString());

			if (job == null) return;
			foreach (var account in AccountManager.AccountsList.FindAll(a => a.IsActive == 0))
			{
				if (job.ProcessList.Count + items.Count >= job.AccountCount)
				{
					break;
				}
				if (account.Valid)
				{
					account.IsActive = 1;
					items.Add(account);
				}
			}
			if (items.Count == 0)
				Logger.Warning($"No more accounts to launch for job {jid}");
			await AccountManager.LaunchAccounts(items, jid, "", new CancellationTokenSource());
		}
	}
}
