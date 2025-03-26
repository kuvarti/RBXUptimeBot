using RestSharp;
using System.Text;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Drawing;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Win32;
using System.Net.NetworkInformation;
using RBXUptimeBot.Classes.Services;
using RBXUptimeBot.Models;
using Microsoft.Extensions.Options;
using System.Configuration;
using Microsoft.EntityFrameworkCore;
using WebSocketSharp;
using RBXUptimeBot.Models.Entities;

namespace RBXUptimeBot.Classes
{
	public class ActiveItem
	{
		public int PID { get; set; }
		public DateTime StartTime { get; set; }
		public Account Account { get; set; }
	};

	public class ActiveJob
	{
		public JobTableEntity JobEntity { get; set; }
		public bool isRunning { get; set; }
		public int AccountCount { get; set; }
		public DateTime startTime { get; set; }
		public DateTime endTime { get; set; }
		public List<ActiveItem> ProcessList { get; set; }
	};

	public class AccountManager
	{
		public static AccountManager Instance;
		public static List<Account> AccountsList;
		public static List<ActiveJob> ActiveJobs;
		public static List<Game> RecentGames;
		public static Account LastValidAccount; // this is used for the Batch class since getting place details requires authorization, auto updates whenever an account is used

		public static RestClient MainClient;
		public static RestClient UsersClient;
		public static RestClient Web13Client;
		public static RestClient AuthClient;

		public static int maxAcc;

		public static PostgreService<JobTableEntity> JobService;
		public static bool isDevelopment = false;

		public static IniFile IniSettings;
		public static IniSection General;
		public static IniSection Machine;
		public static IniSection Watcher;
		public static IniSection Prompts;
		public static Dictionary<string, IniSection> IniList;

		private static Mutex rbxMultiMutex;
		public static string ConnStr { get; set; } = string.Empty;

		public static void AccManagerLoad(string connstr)
		{
			AccountsList = new List<Account>();
			ActiveJobs = new List<ActiveJob>();
			AccountManager.ConnStr = connstr;

			IniSettings = File.Exists(Path.Combine(Environment.CurrentDirectory, "RAMSettings.ini")) ? new IniFile("RAMSettings.ini") : new IniFile();
			General = IniSettings.Section("General");
			Machine = IniSettings.Section("Machine");
			Watcher = IniSettings.Section("Watcher");
			Prompts = IniSettings.Section("Prompts");
			IniList = new Dictionary<string, IniSection> { { "General", General }, { "Machine", Machine }, { "Watcher", Watcher }, { "Prompts", Prompts } };

			MainClient = new RestClient("https://www.roblox.com/");
			UsersClient = new RestClient("https://users.roblox.com");
			Web13Client = new RestClient("https://web.roblox.com/");
			AuthClient = new RestClient("https://auth.roblox.com/");

			JobService = new PostgreService<JobTableEntity>(new DbContextOptionsBuilder<PostgreService<JobTableEntity>>().UseNpgsql(connstr).Options);

			/* MACHINE */
			if (!Machine.Exists("Name")) Machine.Set("Name", "RoBot-1");
			if (!Machine.Exists("ThisMachine")) Machine.Set("ThisMachine", "1");
			if (!Machine.Exists("TotalMachine")) Machine.Set("TotalMachine", "1");
			if (!Machine.Exists("ReLoginEveryTime")) Machine.Set("ReLoginEveryTime", "false");
			if (!Machine.Exists("MaxAccountLoggedIn")) Machine.Set("MaxAccountLoggedIn", isDevelopment ? "2" : "50");
			/* GENERAL */
			if (!General.Exists("JoinDelay")) General.Set("JoinDelay", "60");
			if (!General.Exists("LaunchDelay")) General.Set("LaunchDelay", "60");
			if (!General.Exists("UseProxies")) General.Set("UseProxies", "true");
			if (!General.Exists("CaptchaTimeOut")) General.Set("CaptchaTimeOut", "300");
			if (!General.Exists("Proxifier-Path")) General.Set("Proxifier-Path", "C:\\Program Files (x86)\\Proxifier\\Proxifier.exe");
			if (!General.Exists("BloxstrapTimeout")) General.Set("BloxstrapTimeout", "30");
			if (!General.Exists("ProxifierTimeout")) General.Set("ProxifierTimeout", "90");

			var VCKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\X86");
			if (!Prompts.Exists("VCPrompted") && (VCKey == null || (VCKey is RegistryKey && VCKey.GetValue("Bld") is int VCVersion && VCVersion < 32532)))
				Task.Run(async () => // Make sure the user has the latest 2015-2022 vcredist installed
				{
					using HttpClient Client = new HttpClient();
					byte[] bs = await Client.GetByteArrayAsync("https://aka.ms/vs/17/release/vc_redist.x86.exe");
					string FN = Path.Combine(Path.GetTempPath(), "vcredist.tmp");

					File.WriteAllBytes(FN, bs);

					Process.Start(new ProcessStartInfo(FN) { UseShellExecute = false, Arguments = "/q /norestart" }).WaitForExit();

					Prompts.Set("VCPrompted", "1");
				});

			UpdateMultiRoblox();
			Task.Run(() => { ProxifierService.LoadProxyList(); });
			IniSettings.Save("RAMSettings.ini");
			Logger.Information("Account Manager loaded.");
		}

		public static void ExitProtocol()
		{
			ActiveJobs.ForEach(job =>
			{
				job.isRunning = false;
				job.endTime = DateTime.Now;

			});
			while (true)
			{
				if (ActiveJobs.Count == 0) break;
				Task.Delay(TimeSpan.FromSeconds(1));
			}
			AccountsList.ForEach(acc => acc.LogOutAcc());
			System.Environment.Exit(0);
		}

		public static (bool, string) InitAccounts()
		{
			using (var postgre = new PostgreService<LogTableEntity>(new DbContextOptionsBuilder<PostgreService<LogTableEntity>>().UseNpgsql(ConnStr).Options))
			{
				if (!postgre.Database.CanConnect()) return (false, "Program cannot make connection with POstgresql.");
				ProxifierService.EndProxifiers();
				Task.Run(() => { ProxifierService.LoadProxyList(); });
				LoadAccounts();
			}
			return (true, "Account are loading.");
		}

		private async static void LoadAccounts()
		{
			try
			{
				using (var postgre = new PostgreService<AccountTableEntity>(new DbContextOptionsBuilder<PostgreService<AccountTableEntity>>().UseNpgsql(ConnStr).Options))
				{
					var response = postgre.Table?.AsNoTracking().OrderBy(x => x.ID).ToList();
					if (response == null || response.Count <= 0)
					{
						Logger.Information($"No account data found from postgre.");
						return;
					}
					maxAcc = response.Count;
					foreach (var item in response)
					{
						if (!item.State.IsNullOrEmpty() && item.State.StartsWith("FATAL:"))
							continue;
						Account account = AccountsList.Find(acc => acc.ID == item.ID);
						if (account != null) await account.CheckTokenAndLoginIsNotValid();
						else
						{
							try
							{
								account = new Account(item)
								{
									Username = item.Username,
									Password = item.Password,
									SecurityToken = item.Token
								};
								await account.CheckTokenAndLoginIsNotValid();
								if (account.Valid) AccountsList.Add(account);
							}
							catch (Exception ex)
							{
								Logger.Error($"Error while creating account.", ex);
							}
						}
						if (AccountsList.Count >= Machine.Get<int>("MaxAccountLoggedIn")) break;
					}
					postgre.Dispose();
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"Error while read data from postgre. Accounts will not be loaded.", ex);
			}
		}

		public static bool UpdateMultiRoblox()
		{
			try
			{
				rbxMultiMutex = new Mutex(true, "ROBLOX_singletonMutex");
				if (!rbxMultiMutex.WaitOne(TimeSpan.Zero, true))
					return false;
			}
			catch { return false; }
			return true;
		}

		public static bool GetUserID(string Username, out long UserId, out RestResponse response)
		{
			RestRequest request = LastValidAccount?.MakeRequest("v1/usernames/users", Method.Post) ?? new RestRequest("v1/usernames/users", Method.Post);
			request.AddJsonBody(new { usernames = new string[] { Username } });

			response = UsersClient.Execute(request);

			if (response.StatusCode == HttpStatusCode.OK && response.Content.TryParseJson(out JObject UserData) && UserData.ContainsKey("data") && UserData["data"].Count() >= 1)
			{
				UserId = UserData["data"]?[0]?["id"].Value<long>() ?? -1;

				return true;
			}

			UserId = -1;

			return false;
		}

		public static async Task<string> LogoutAccount(string accounts)
		{
			int i = 0;
			if (AccountManager.AccountsList.Count == 0) return "Account list is already empty.";
			List<string> ComboList = new List<string>(accounts.Split('\n'));
			if (ComboList.Count == 1) ComboList = new List<string>(accounts.Split('-'));

			ComboList.ForEach(ComboList =>
			{
				var lst = AccountManager.AccountsList.FindAll(acc => acc.Username == ComboList);
				if (lst.Any())
				{
					lst.ForEach(acc =>
					{
						i += AccountManager.AccountsList.Remove(acc) ? 1 : 0;
						Logger.Information($"Account '{acc.Username}' is logged out.");
					});
				}
			});
			//SaveAccounts();
			return $"{i} accounts logged out.";
		}

		public static async Task<List<ActiveItem>> LaunchAccounts(List<Account> Accounts, long PlaceID, string JobID, CancellationTokenSource token = null, bool FollowUser = false, bool VIPServer = false)
		{
			CancellationTokenSource Token = token;
			List<ActiveItem> items = new List<ActiveItem>();

			bool AsyncJoin = General.Get<bool>("AsyncJoin");

			foreach (Account account in Accounts)
			{
				if (Token.IsCancellationRequested) break;

				long PlaceId = PlaceID;
				string JobId = JobID;

				if (!FollowUser)
				{
					if (!string.IsNullOrEmpty(account.GetField("SavedPlaceId")) && long.TryParse(account.GetField("SavedPlaceId"), out long PID)) PlaceId = PID;
					if (!string.IsNullOrEmpty(account.GetField("SavedJobId"))) JobId = account.GetField("SavedJobId");
				}
				if (!AccountManager.ActiveJobs.Find(job => job.JobEntity.PlaceID == PlaceID.ToString()).isRunning)
					break;
				new Thread(async () =>
				{
					await account.JoinServer(PlaceId, JobId, FollowUser, VIPServer);
					if (account.IsActive <= 10)
						account.IsActive = 0;
				}).Start();
				await Task.Delay(TimeSpan.FromSeconds(General.Get<int>("JoinDelay")));
				//await RBXUptimeBot.Classes.Services.JobService.ControlJobs(PlaceID);
			}
			Token.Cancel();
			Token.Dispose();
			return items;
		}
	}
}
