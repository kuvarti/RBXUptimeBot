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
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using System.Configuration;
using Google.Apis.Sheets.v4;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4.Data;

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
		public long Jid { get; set; }
		public bool isRunning { get; set; }
		public int AccountCount { get; set; }
		public string DBid { get; set; }
		public DateTime startTime { get; set; }
		public DateTime endTime { get; set; }
		public List<ActiveItem> ProcessList { get; set; }
	};

	public class AccountManager
	{
		public static AccountManager Instance;
		public static List<Account> AccountsList;
		public static List<ActiveItem> AllRunningAccounts;
		public static List<ActiveJob> ActiveJobs;
		public static List<Game> RecentGames;
		public static Account LastValidAccount; // this is used for the Batch class since getting place details requires authorization, auto updates whenever an account is used

		public static RestClient MainClient;
		public static RestClient AuthClient;
		public static RestClient UsersClient;
		public static RestClient Web13Client;
		public static SheetsService SheetsService;

		public static IMongoDbService<LogEntry> LogService;

		public static bool isDevelopment = false;

		public static IniFile IniSettings;
		public static IniSection General;
		public static IniSection Machine;
		public static IniSection Watcher;
		public static IniSection Prompts;
		public static IniSection GSheet;

		private static Mutex rbxMultiMutex;

		public static void AccManagerLoad()
		{
			AccountsList = new List<Account>();
			AllRunningAccounts = new List<ActiveItem>();
			ActiveJobs = new List<ActiveJob>();

			IniSettings = File.Exists(Path.Combine(Environment.CurrentDirectory, "RAMSettings.ini")) ? new IniFile("RAMSettings.ini") : new IniFile();
			General = IniSettings.Section("General");
			Machine = IniSettings.Section("Machine");
			Watcher = IniSettings.Section("Watcher");
			Prompts = IniSettings.Section("Prompts");
			GSheet = IniSettings.Section("GSheet");

			MainClient = new RestClient("https://www.roblox.com/");
			AuthClient = new RestClient("https://auth.roblox.com/");
			UsersClient = new RestClient("https://users.roblox.com");
			Web13Client = new RestClient("https://web.roblox.com/");

			/* MACHINE */
			if (!Machine.Exists("Name")) Machine.Set("Name", "RoBot-1");
			if (!Machine.Exists("ThisMachine")) Machine.Set("ThisMachine", "1");
			if (!Machine.Exists("TotalMachine")) Machine.Set("TotalMachine", "1");
			if (!Machine.Exists("MaxAccountLoggedIn")) Machine.Set("MaxAccountLoggedIn", isDevelopment ? "2" : "50");
			/* GENERAL */
			if (!General.Exists("JoinDelay")) General.Set("JoinDelay", "60");
			if (!General.Exists("LaunchDelay")) General.Set("LaunchDelay", "60");
			if (!General.Exists("UseProxies")) General.Set("UseProxies", "true");
			if (!General.Exists("CaptchaTimeOut")) General.Set("CaptchaTimeOut", "300");
			if (!General.Exists("Proxifier-Path")) General.Set("Proxifier-Path", "C:\\Program Files (x86)\\Proxifier\\Proxifier.exe");
			if (!General.Exists("BloxstrapTimeout")) General.Set("BloxstrapTimeout", "30");

			// BU AMK UYGULAMASINI KESKE CLONELAYIP DUZENLEMEK YERINE 0'DAN YAPSAYDIM DA SOYLE UCUBE UCUBE SEYLER YAPMAK ZORUNDA KALMASAYDIM
			try
			{
				var configuration = new ConfigurationBuilder()
					.SetBasePath(Directory.GetCurrentDirectory())
					.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
					.Build();
				var mongoSettings = new MongoDBSettings();
				configuration.GetSection("MongoDBSettings").Bind(mongoSettings);
				var options = Options.Create(mongoSettings);
				LogService = new MongoDbService<LogEntry>(options);

				if (LogService.IsConnected())
				{
					LogService.CreateAsync(Logger.Information($"App started. {DateTime.Now.ToString()}")).GetAwaiter().GetResult();
				}
			}
			catch (Exception ex)
			{
				Logger.Critical($"Error creating MongoDbService: {ex}");
			}

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
			IniSettings.Save("RAMSettings.ini");
			if (InitGoogleSheets())
			{
				ProxifierService.EndProxifiers();
				Task.Run(() => { ProxifierService.LoadProxyList(); });
				LoadAccounts();
			}
		}

		private static bool InitGoogleSheets()
		{
			if (!GSheet.Exists("APIKeyFile") || !GSheet.Exists("SpreadsheetId"))
			{
				LogService.CreateAsync(Logger.Error($"APIKeyFile or SpreadsheetId information not exist. Accounts will not be loaded.")).GetAwaiter().GetResult();
				return false;
			}

			try
			{
				string[] Scopes = { SheetsService.Scope.Spreadsheets };
				using var stream = new FileStream(GSheet.Get<string>("APIKeyFile"), FileMode.Open, FileAccess.Read);
				var credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
				SheetsService = new SheetsService(new BaseClientService.Initializer()
				{
					HttpClientInitializer = credential,
				});
				SheetsService.Spreadsheets.Get(GSheet.Get<string>("SpreadsheetId")).Execute();
			}
			catch (Exception ex)
			{
				LogService.CreateAsync(Logger.Error($"Error while connect google api. Accounts will not be loaded.", ex)).GetAwaiter().GetResult();
				return false;
			}
			return true;
		}

		private async static void LoadAccounts()
		{
			if (!GSheet.Exists("AccountsTableName"))
			{
				LogService.CreateAsync(Logger.Error($"AccountsTableName information not exist. Accounts will not be loaded.")).GetAwaiter().GetResult();
				return;
			}

			try
			{
				string SpreadsheetId = GSheet.Get<string>("SpreadsheetId");
				string AccountsTableName = GSheet.Get<string>("AccountsTableName");
				var response = SheetsService.Spreadsheets.Values.Get(SpreadsheetId, AccountsTableName).Execute();

				var values = response.Values;
				if (values == null && values.Count <= 0)
				{
					LogService.CreateAsync(Logger.Information($"No account data found from google api.")).GetAwaiter().GetResult();
					return;
				}
				for (int i = 1; i < values.Count; i++)
				{
					var item = values[i];
					if (//item[5].ToString() != "Standby" || //!(item[5].ToString() != $"Logged in on {Machine.Get<string>("Name")}") ||
						item[5].ToString().StartsWith("FATAL:"))
						continue;
					Account account = AccountsList.Find(acc => acc.Username == item[1].ToString());
					if (account != null) await account.CheckTokenAndLoginIsNotValid();
					else
					{
						account = new Account(item[6].ToString())
						{
							Row = Convert.ToInt16(item[0]),
							Username = item[1]?.ToString(),
							Password = item[2]?.ToString(),
							SecurityToken = item[3]?.ToString()
						};
						await account.CheckTokenAndLoginIsNotValid();
						if (account.Valid) AccountsList.Add(account);
					}
					if (AccountsList.Count >= Machine.Get<int>("MaxAccountLoggedIn")) break;
				}
			}
			catch (Exception ex)
			{
				LogService.CreateAsync(Logger.Error($"Error while read data from google api. Accounts will not be loaded.", ex)).GetAwaiter().GetResult();
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
				if (!AccountManager.ActiveJobs.Find(job => job.Jid == PlaceID).isRunning)
					break;
				new Thread(async () =>
				{
					await account.JoinServer(PlaceId, JobId, FollowUser, VIPServer);
					if (account.IsActive <= 10)
						account.IsActive = 0;
				}).Start();
				await Task.Delay(TimeSpan.FromSeconds(General.Get<int>("JoinDelay")));
			}
			Token.Cancel();
			Token.Dispose();
			return items;
		}
	}
}
