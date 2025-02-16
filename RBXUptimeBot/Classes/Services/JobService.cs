using Microsoft.Extensions.Options;
using PuppeteerExtraSharp;
using RBXUptimeBot.Models;
using System.Diagnostics;
using System.Security.Cryptography;

namespace RBXUptimeBot.Classes.Services
{
	public class JobService
	{
		public readonly IMongoDbService<JobEntry> _JobService;

		public JobService() {
			var configuration = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.Build();
			var mongoSettings = new MongoDBSettings();
			configuration.GetSection("MongoDBSettings").Bind(mongoSettings);
			var options = Options.Create(mongoSettings);
			_JobService = new MongoDbService<JobEntry>(options);
		}

		public async Task<string> JobStarter(long jId, int accCount, DateTime end)
		{
			if (AccountManager.AccountsList.Count - AccountManager.AllRunningAccounts.Count < accCount)
				return $"Not enough accounts to start the job.({AccountManager.AccountsList.Count})";
			var job = new JobEntry() {
				placeId = jId.ToString(),
				AccCount = accCount,
				StartTime = DateTime.Now,
				EndTime = null,
				Description = null,
			};
			_JobService.CreateAsync(job).GetAwaiter().GetResult();
			AccountManager.ActiveJobs.Add(new ActiveJob()
			{
				Jid = jId,
				AccountCount = accCount,
				startTime = DateTime.Now,
				endTime = end,
				DBid = job.Id,
				ProcessList = new List<ActiveItem>()
			});
			new Thread(async () => await JobController(jId)).Start();

			if (AccountManager.LogService == null) Logger.Trace($"job {jId} started in {DateTime.Now}");
			AccountManager.LogService?.CreateAsync(Logger.Trace($"job {jId} started in {DateTime.Now}"));
			return $"Job {jId} started.";
		}

		public async Task JobFinisher(long jid)
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

			_JobService.UpdateAsync(job.DBid, new JobEntry()
			{
				placeId = jid.ToString(),
				AccCount = job.AccountCount,
				StartTime = job.startTime,
				EndTime = DateTime.Now,
				Description = null
			}).GetAwaiter().GetResult();
			if (AccountManager.LogService == null) Logger.Trace($"job {jid} is finished {DateTime.Now}");
			AccountManager.LogService?.CreateAsync(Logger.Information($"Job {jid} is finished {DateTime.Now}."));

		}

		public async Task JobController(long jid)
		{
			ActiveJob job = AccountManager.ActiveJobs.Find(x => x.Jid == jid);
			List<Account> accounts = new List<Account>();
			if (job == null) return;
			job.isRunning = true;
			while (DateTime.Now < job.endTime)
			{
				foreach (var item in job.ProcessList)
				{
					if (Process.GetProcessById(item.PID).MainWindowTitle != "Roblox") {
						AccountManager.LogService.CreateAsync(Logger.Error($"Something is wrong with client {item.Account.Username}: Main Window title isnt 'Roblox'"));
						accounts.Add(item.Account);
					}
				}
				foreach (var account in accounts){
					account.LeaveServer();
					job.ProcessList.RemoveAll(ritem => ritem.Account == account);
				}
				accounts.Clear();
				await Task.Delay(5000);
				if (job.AccountCount > job.ProcessList.Count)
				{
					await AddProcess(jid);
				}
				if (!job.isRunning)
					break;
			}
			JobFinisher(jid);
		}

		// Assingning early bc; it can be used from another process while running
		public async Task AddProcess(long jid)
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

		public async Task RestartProcess(long jid, ActiveItem a)
		{
			if (a.Account.IsActive == 0) return;
			a.Account.LeaveServer();
			await Task.Delay(60000);
			a.Account.JoinServer(jid);
		}
	}
}
