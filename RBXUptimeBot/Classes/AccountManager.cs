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
		public static RestClient MainClient; //DIS
		public static RestClient AvatarClient;
		public static RestClient FriendsClient;
		public static RestClient UsersClient;
		public static RestClient AuthClient;
		public static RestClient EconClient;
		public static RestClient AccountClient;
		public static RestClient GameJoinClient;
		public static RestClient Web13Client;

		public static IMongoDbService<LogEntry> LogService;
		//TODO job service

		public static string CurrentVersion;
		private readonly static DateTime startTime = DateTime.Now;

		public static bool IsTeleport = false;
		public static bool UseOldJoin = false;
		public static bool ShuffleJobID = false;

		public static IniFile IniSettings;
		public static IniSection General;
		public static IniSection Developer;
		public static IniSection WebServer;
		public static IniSection AccountControl;
		public static IniSection Watcher;
		public static IniSection Prompts;

		private static Mutex rbxMultiMutex;
		private readonly static object saveLock = new object();
		private readonly static object rgSaveLock = new object();
		public event EventHandler<GameArgs> RecentGameAdded;

		private bool IsResettingPassword;
		private bool IsDownloadingChromium;
		private bool LaunchNext;
		private CancellationTokenSource LauncherToken;

		private static readonly byte[] Entropy = new byte[] { 0x52, 0x4f, 0x42, 0x4c, 0x4f, 0x58, 0x20, 0x41, 0x43, 0x43, 0x4f, 0x55, 0x4e, 0x54, 0x20, 0x4d, 0x41, 0x4e, 0x41, 0x47, 0x45, 0x52, 0x20, 0x7c, 0x20, 0x3a, 0x29, 0x20, 0x7c, 0x20, 0x42, 0x52, 0x4f, 0x55, 0x47, 0x48, 0x54, 0x20, 0x54, 0x4f, 0x20, 0x59, 0x4f, 0x55, 0x20, 0x42, 0x55, 0x59, 0x20, 0x69, 0x63, 0x33, 0x77, 0x30, 0x6c, 0x66 };

		private static ReadOnlyMemory<byte> PasswordHash;
		private readonly static string SaveFilePath = Path.Combine(Environment.CurrentDirectory, "AccountData.json");
		private readonly static string RecentGamesFilePath = Path.Combine(Environment.CurrentDirectory, "RecentGames.json");

		public static void AccManagerLoad()
		{
			IniSettings = File.Exists(Path.Combine(Environment.CurrentDirectory, "RAMSettings.ini")) ? new IniFile("RAMSettings.ini") : new IniFile();
			General = IniSettings.Section("General");
			Developer = IniSettings.Section("Developer");
			WebServer = IniSettings.Section("WebServer");
			AccountControl = IniSettings.Section("AccountControl");
			Watcher = IniSettings.Section("Watcher");
			Prompts = IniSettings.Section("Prompts");

			MainClient = new RestClient("https://www.roblox.com/");
			AvatarClient = new RestClient("https://avatar.roblox.com/");
			AuthClient = new RestClient("https://auth.roblox.com/");
			EconClient = new RestClient("https://economy.roblox.com/");
			AccountClient = new RestClient("https://accountsettings.roblox.com/");
			GameJoinClient = new RestClient(new RestClientOptions("https://gamejoin.roblox.com/") { UserAgent = "Roblox/WinInet" });
			UsersClient = new RestClient("https://users.roblox.com");
			FriendsClient = new RestClient("https://friends.roblox.com");
			Web13Client = new RestClient("https://web.roblox.com/");

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

			AccountsList = new List<Account>();
			AllRunningAccounts = new List<ActiveItem>();
			ActiveJobs = new List<ActiveJob>();

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

			LoadAccounts();
			UpdateMultiRoblox();
		}

		public void NextAccount() => LaunchNext = true;

		public static Account AddAccount(string SecurityToken, string Password = "", string AccountJSON = null)
		{
			Account account = new Account(SecurityToken, AccountJSON);
			if (account.Valid)
			{
				account.Password = Password;
				Account exists = AccountsList.AsReadOnly().FirstOrDefault(acc => acc.UserID == account.UserID);
				if (exists != null)
				{
					account = exists;
					exists.SecurityToken = SecurityToken;
					exists.Password = Password;
					exists.LastUse = DateTime.Now;
				}
				else
				{
					account.LastUse = DateTime.Now;
					AccountsList.Add(account);
				}
				SaveAccounts(true);
				return account;
			}
			return null;
		}

		public static void SaveAccounts(bool BypassRateLimit = false, bool BypassCountCheck = false)
		{
			if ((!BypassRateLimit && (DateTime.Now - startTime).Seconds < 2) || (!BypassCountCheck && AccountsList.Count == 0)) return;

			lock (saveLock)
			{
				byte[] OldInfo = File.Exists(SaveFilePath) ? File.ReadAllBytes(SaveFilePath) : Array.Empty<byte>();
				string SaveData = JsonConvert.SerializeObject(AccountsList);

				FileInfo OldFile = new FileInfo(SaveFilePath);
				FileInfo Backup = new FileInfo($"{SaveFilePath}.backup");

				if (!Backup.Exists || (Backup.Exists && (DateTime.Now - Backup.LastWriteTime).TotalMinutes > 60 * 8))
					File.WriteAllBytes(Backup.FullName, OldInfo);

				File.WriteAllBytes(SaveFilePath, Encoding.UTF8.GetBytes(SaveData));
			}
		}

		private static void LoadAccounts(byte[] Hash = null)
		{
			bool EnteredPassword = false;
			byte[] Data = File.Exists(SaveFilePath) ? File.ReadAllBytes(SaveFilePath) : Array.Empty<byte>();

			if (Data.Length > 0)
			{
				var Header = new ReadOnlySpan<byte>(Data, 0, Cryptography.RAMHeader.Length);

				if (Header.SequenceEqual(Cryptography.RAMHeader))
				{
					if (Hash == null) return;

					Data = Cryptography.Decrypt(Data, Hash);
					AccountsList = JsonConvert.DeserializeObject<List<Account>>(Encoding.UTF8.GetString(Data));
					PasswordHash = new ReadOnlyMemory<byte>(ProtectedData.Protect(Hash, Array.Empty<byte>(), DataProtectionScope.CurrentUser));

					EnteredPassword = true;
				}
				else
					try { AccountsList = JsonConvert.DeserializeObject<List<Account>>(Encoding.UTF8.GetString(ProtectedData.Unprotect(Data, Entropy, DataProtectionScope.CurrentUser))); }
					catch (CryptographicException e)
					{
						try { AccountsList = JsonConvert.DeserializeObject<List<Account>>(Encoding.UTF8.GetString(Data)); }
						catch
						{
							File.WriteAllBytes(SaveFilePath + ".bak", Data);
							Logger.Error($"Failed to load accounts!\nA backup file was created in case the data can be recovered.\n\n{e.Message}", e);
						}
					}
			}

			AccountsList ??= new List<Account>();

			if (!EnteredPassword && AccountsList.Count == 0 && File.Exists($"{SaveFilePath}.backup") && File.ReadAllBytes($"{SaveFilePath}.backup") is byte[] BackupData && BackupData.Length > 0)
			{
				var Header = new ReadOnlySpan<byte>(BackupData, 0, Cryptography.RAMHeader.Length);
			}

			if (AccountsList.Count > 0)
			{
				LastValidAccount = AccountsList[0];

				foreach (Account account in AccountsList)
					if (account.LastUse > LastValidAccount.LastUse)
						LastValidAccount = account;
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

		public void CancelLaunching()
		{
			if (LauncherToken != null && !LauncherToken.IsCancellationRequested)
				LauncherToken.Cancel();
		}

		public static async Task LoginAccount(string accounts)
		{
			string Combos = accounts;
			Logger.Information($"{DateTime.Now} attempt login accounts");

			if (Combos == "/UC") return;

			List<string> ComboList = new List<string>(Combos.Split('\n'));
			if (ComboList.Count == 1) ComboList = new List<string>(Combos.Split('-'));

			var Size = new System.Numerics.Vector2(455, 485);
			//AccountBrowser.CreateGrid(Size);

			for (int i = 0; i < ComboList.Count; i++)
			{
				string Combo = ComboList[i];

				if (!Combo.Contains(':')) continue;
				
				string username = Combo.Substring(0, Combo.IndexOf(':'));

				if (AccountManager.AccountsList.Find(x => x.Username == username) != null) {
					Logger.Warning($"Account '{username}' already signed in.");
					continue;
				}

				var accountBrowser = new AccountBrowser() { Index = i, Size = Size };
				await accountBrowser.Login(username, Combo.Substring(Combo.IndexOf(":") + 1));
			}
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
				if (lst.Any()) {
					lst.ForEach(acc => {
						i += AccountManager.AccountsList.Remove(acc) ? 1 : 0;
						Logger.Information($"Account '{acc.Username}' is logged out.");
					});
				}
			});
			return $"{i} accounts logged out.";
		}

		public static async Task<List<ActiveItem>> LaunchAccounts(List<Account> Accounts, long PlaceID, string JobID, CancellationTokenSource token = null, bool FollowUser = false, bool VIPServer = false)
		{
			CancellationTokenSource Token = token;
			List<ActiveItem> items = new List<ActiveItem>();
			int Delay = General.Exists("AccountJoinDelay") ? General.Get<int>("AccountJoinDelay") : 8;

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
				new Thread(async () =>
				{
					await account.JoinServer(PlaceId, JobId, FollowUser, VIPServer);
				}).Start();
				await Task.Delay(Delay * 1000);
			}
			Token.Cancel();
			Token.Dispose();
			return items;
		}
	}
}
