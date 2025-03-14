using Google.Apis.Sheets.v4.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RBXUptimeBot.Classes.Services;
using RBXUptimeBot.Models;
using RBXUptimeBot.Models.Entities;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using WebSocketSharp;
using static RBXUptimeBot.Classes.RobloxWatcher;

namespace RBXUptimeBot.Classes
{
	public partial class Account
	{
		public int ID { get; init; }
		public bool Valid { get; set; }
		private string _Ticket;
		private int _isActive;
		public int IsActive
		{
			get => _isActive;
			set
			{
				_isActive = value;
				string State = default(string);
				if (value == 0) State = "Standby";
				else if (value == 1) State = "Join Queue";
				else if (value > 10) State = "In Job";
				else State = "Unknown";
				if (Valid) UpdateStatus(State);
			}
		}
		public int Row { get; init; }
		public string Username { get; init; }
		public string Password { get; init; }
		private string _Token;
		public string SecurityToken
		{
			get => _Token;
			set
			{
				_Token = value;
				if (Valid) UpdateToken(value);
				if (value.IsNullOrEmpty()) UpdateStatus(string.Empty);
			}
		}
		private AccountTableEntity Entity;
		private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
		private static readonly SemaphoreSlim instancecheck = new SemaphoreSlim(1, 1);
		private ProxifierService PS;
		public long UserID;
		public Dictionary<string, string> Fields = new Dictionary<string, string>();
		[JsonIgnore] public DateTime TokenSet;
		[JsonIgnore] public string CSRFToken;

		[DllImport("user32.dll", SetLastError = true)]
		static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

		private RestClient AuthClient;
		public string BrowserTrackerID;

		public Account(AccountTableEntity _entity)
		{
			Entity = _entity;
			Valid = false;
			IsActive = 0;

			PS = new ProxifierService(_entity.Proxy);
			AuthClient = new RestClient(new RestClientOptions("https://auth.roblox.com/")
			{
				Proxy = new WebProxy($"http://{PS.Proxy.ProxyIP}:{PS.Proxy.ProxyPort}", false)
				{
					Credentials = new NetworkCredential(PS.Proxy.ProxyUsername, PS.Proxy.ProxyPassword)
				}
			});
		}

		public async Task CheckTokenAndLoginIsNotValid()
		{
			PS.LaunchProcess(); await Wait4Proxyfier();
			if (AccountManager.Machine.Get<bool>("ReLoginEveryTime") || (!GetCSRFToken(out string _, true) && !GetAuthTicket(out string _, true)))
			{
				AccountBrowser acbrowser = new AccountBrowser() { Size = new System.Numerics.Vector2(455, 485) };

				var loginRes = await acbrowser.Login(Username, Password);
				if (loginRes.Success)
				{
					Valid = true;
					SecurityToken = loginRes.Message;
					UpdateState($"Logged in on {AccountManager.Machine.Get<string>("Name")}.");
				}
				else
				{
					SecurityToken = "";
					UpdateState($"{loginRes.ErrorType}: {loginRes.Message}");
				}
			}
			else
			{
				Valid = true;
				UpdateState($"Logged in on {AccountManager.Machine.Get<string>("Name")}.");
			}
			PS.EndProcess();
			if (Valid) IsActive = 0;
			else SecurityToken = "";
		}

		public RestRequest MakeRequest(string url, Method method = Method.Get) => new RestRequest(url, method).AddCookie(".ROBLOSECURITY", SecurityToken, "/", ".roblox.com");

		public bool GetAuthTicket(out string Ticket, bool useOriginal = false)
		{
			Ticket = string.Empty;

			if (!GetCSRFToken(out string Token, useOriginal)) return false;

			RestRequest request = MakeRequest("v1/authentication-ticket/", Method.Post)
				.AddHeader("X-CSRF-TOKEN", Token).AddHeader("Referer", "https://www.roblox.com/games/75965306756161/Color-io")
				.AddHeader("Content-type", "application/json");

			RestResponse response = useOriginal ? AccountManager.AuthClient.Execute(request) : AuthClient.Execute(request);

			Parameter TicketHeader = response.Headers.FirstOrDefault(x => x.Name == "rbx-authentication-ticket");

			if (TicketHeader != null)
			{
				Ticket = (string)TicketHeader.Value;

				return true;
			}

			return false;
		}
		//TODO these two can be combined into one method???
		public bool GetCSRFToken(out string Result, bool useOriginal = false)
		{
			RestRequest request = MakeRequest("v1/authentication-ticket/", Method.Post).AddHeader("Referer", "https://www.roblox.com/games/75965306756161/Color-io").AddHeader("Content-type", "application/json"); ;

			RestResponse response = useOriginal ? AccountManager.AuthClient.Execute(request) : AuthClient.Execute(request);

			if (response.StatusCode != HttpStatusCode.Forbidden)
			{
				Result = $"[{(int)response.StatusCode} {response.StatusCode}] {response.Content}";
				return false;
			}

			Parameter result = response.Headers.FirstOrDefault(x => x.Name == "x-csrf-token");
			string Token = string.Empty;

			if (result != null)
			{
				Token = (string)result.Value;
				AccountManager.LastValidAccount = this;
			}

			CSRFToken = Token;
			TokenSet = DateTime.Now;
			Result = Token;
			return !string.IsNullOrEmpty(Result);
		}

		public async Task<JToken> GetMobileInfo()
		{
			RestRequest DataRequest = MakeRequest("mobileapi/userinfo", Method.Get);

			RestResponse response = await AccountManager.MainClient.ExecuteAsync(DataRequest);

			if (response.StatusCode == HttpStatusCode.OK && Utilities.TryParseJson(response.Content, out JToken Data))
				return Data;

			return null;
		}

		public bool LogOutOfOtherSessions(bool Internal = false)
		{
			if (!GetCSRFToken(out string Token)) return false;

			RestRequest request = MakeRequest("authentication/signoutfromallsessionsandreauthenticate", Method.Post)
				.AddHeader("Referer", "https://www.roblox.com/")
				.AddHeader("X-CSRF-TOKEN", Token)
				.AddHeader("Content-Type", "application/x-www-form-urlencoded");

			RestResponse response = AccountManager.MainClient.Execute(request);

			if (response.IsSuccessful && response.StatusCode == HttpStatusCode.OK)
			{
				var SToken = response.Cookies[".ROBLOSECURITY"];
				if (SToken != null)
				{
					SecurityToken = SToken.Value;
					//AccountManager.SaveAccounts(true);
				}
				return true;
			}
			return false;
		}

		public bool ParseAccessCode(RestResponse response, out string Code)
		{
			Code = "";
			Match match = Regex.Match(response.Content, "Roblox.GameLauncher.joinPrivateGame\\(\\d+\\,\\s*'(\\w+\\-\\w+\\-\\w+\\-\\w+\\-\\w+)'");
			if (match.Success && match.Groups.Count == 2)
			{
				Code = match.Groups[1]?.Value ?? string.Empty;
				return true;
			}
			return false;
		}

		private async Task Wait4Proxyfier()
		{
			Process p = null;
			while (p == null)
			{
				var pl = Process.GetProcessesByName("Proxifier");
				if (pl != null) p = pl.FirstOrDefault();
				await Task.Delay(TimeSpan.FromSeconds(5));
			}
		}

		public async Task InstanceCheck()
		{
			await instancecheck.WaitAsync();
			Process process = null;

			try { process = Process.GetProcessById(this.IsActive); } catch { }
			if (process != null) {
				if (CheckTicket(process.GetCommandLine(), _Ticket)) {
					instancecheck.Release();
					return;	
				}
			}
			else {
				var ps = Process.GetProcessesByName("RobloxPlayerBeta");
				foreach (var item in ps)
				{
					if (CheckTicket(process.GetCommandLine(), _Ticket))
					{
						this.IsActive = item.Id;
						instancecheck.Release();
						return;
					}
				}
			}
		}

		private async Task<Process> Wait4Roblox(string ticket)
		{
			int i = 0, imax = AccountManager.General.Get<int>("BloxstrapTimeout");
			while (true)
			{
				var rbx = Process.GetProcessesByName("RobloxPlayerBeta");
				foreach (var process in rbx)
				{
					if (CheckTicket(process.GetCommandLine(), ticket))
					{
						return process;
					}
				}
				i++; if (i > imax) break;
				await Task.Delay(TimeSpan.FromSeconds(1));
			}
			var blox = Process.GetProcessesByName("Bloxstrap");
			foreach (var process in blox)
			{
				process.Kill();
			}
			return null;
		}

		public async Task JoinServer(long PlaceID, string JobID = "", bool FollowUser = false, bool JoinVIP = false, bool Internal = false) // oh god i am not refactoring everything to be async im sorry
		{
			PS.LaunchProcess();
			await Wait4Proxyfier();
			if (string.IsNullOrEmpty(BrowserTrackerID))
			{
				Random r = new Random();
				BrowserTrackerID = r.Next(100000, 175000).ToString() + r.Next(100000, 900000).ToString(); // oh god this is ugly
			}

			if (!GetCSRFToken(out string Token, true))
			{
				LoginFailedProcedure($"ERROR: Account {Username} Session Expired, re-add the account or try again. (Invalid X-CSRF-Token)\n{Token}");
				await AccountManager.LogoutAccount(Username);
				return;
			}

			if (!GetAuthTicket(out string Ticket, true))
			{
				LoginFailedProcedure("ERROR: Invalid Authentication Ticket, re-add the account or try again\n(Failed to get Authentication Ticket, Roblox has probably signed you out)");
				return;
			}

			if (AccountManager.General.Get<bool>("AutoCloseLastProcess"))
			{
				try
				{
					foreach (Process proc in Process.GetProcessesByName("RobloxPlayerBeta"))
					{
						var TrackerMatch = Regex.Match(proc.GetCommandLine(), @"\-b (\d+)");
						string TrackerID = TrackerMatch.Success ? TrackerMatch.Groups[1].Value : string.Empty;
						if (TrackerID == BrowserTrackerID)
						{
							try // ignore ObjectDisposedExceptions
							{
								proc.CloseMainWindow();
								await Task.Delay(250);
								proc.CloseMainWindow(); // Allows Roblox to disconnect from the server so we don't get the "Same account launched" error
								await Task.Delay(250);
								proc.Kill();
							}
							catch { }
						}
					}
				}
				catch (Exception x) { Logger.Error($"An error occured attempting to close {Username}'s last process(es): {x}", x); }
			}

			string LinkCode = string.IsNullOrEmpty(JobID) ? string.Empty : Regex.Match(JobID, "privateServerLinkCode=(.+)")?.Groups[1]?.Value;
			string AccessCode = JobID;

			if (!string.IsNullOrEmpty(LinkCode))
			{
				RestRequest request = MakeRequest(string.Format("/games/{0}?privateServerLinkCode={1}", PlaceID, LinkCode), Method.Get).AddHeader("X-CSRF-TOKEN", Token).AddHeader("Referer", "https://www.roblox.com/games/4924922222/Brookhaven-RP");

				RestResponse response = await AccountManager.MainClient.ExecuteAsync(request);

				if (response.StatusCode == HttpStatusCode.OK)
				{
					if (ParseAccessCode(response, out string Code))
					{
						JoinVIP = true;
						AccessCode = Code;
					}
				}
				else if (response.StatusCode == HttpStatusCode.Redirect) // thx wally (p.s. i hate wally)
				{
					request = MakeRequest(string.Format("/games/{0}?privateServerLinkCode={1}", PlaceID, LinkCode), Method.Get).AddHeader("X-CSRF-TOKEN", Token).AddHeader("Referer", "https://www.roblox.com/games/4924922222/Brookhaven-RP");

					RestResponse result = await AccountManager.Web13Client.ExecuteAsync(request);

					if (result.StatusCode == HttpStatusCode.OK)
					{
						if (ParseAccessCode(response, out string Code))
						{
							JoinVIP = true;
							AccessCode = Code;
						}
					}
				}
			}

			double LaunchTime = Math.Floor((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds * 1000);

			await Task.Run(async () => // prevents roblox launcher hanging our main process
			{
				await semaphore.WaitAsync();
				Process Launcher = null;
				var job = AccountManager.ActiveJobs.Find(item => item.Jid == PlaceID);
				if (job == null)
				{
					LoginFailedProcedure($"JobID({PlaceID}) cannot found while process start ({this.Username}). Process will be aborted");
					return;
				}

				try
				{
					ProcessStartInfo LaunchInfo = new ProcessStartInfo();
					LaunchInfo.UseShellExecute = true;

					if (JoinVIP)
						LaunchInfo.FileName = $"roblox-player:1+launchmode:play+gameinfo:{Ticket}+launchtime:{LaunchTime}+placelauncherurl:{HttpUtility.UrlEncode($"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestPrivateGame&placeId={PlaceID}&accessCode={AccessCode}&linkCode={LinkCode}")}+browsertrackerid:{BrowserTrackerID}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
					else if (FollowUser)
						LaunchInfo.FileName = $"roblox-player:1+launchmode:play+gameinfo:{Ticket}+launchtime:{LaunchTime}+placelauncherurl:{HttpUtility.UrlEncode($"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestFollowUser&userId={PlaceID}")}+browsertrackerid:{BrowserTrackerID}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
					else
						LaunchInfo.FileName = $"roblox-player:1+launchmode:play+gameinfo:{Ticket}+launchtime:{LaunchTime}+placelauncherurl:{HttpUtility.UrlEncode($"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame{(string.IsNullOrEmpty(JobID) ? "" : "Job")}&browserTrackerId={BrowserTrackerID}&placeId={PlaceID}{(string.IsNullOrEmpty(JobID) ? "" : ("&gameId=" + JobID))}&isPlayTogetherGame=false")}+browsertrackerid:{BrowserTrackerID}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";

					try { Launcher = Process.Start(LaunchInfo); }
					catch (Exception e) { Logger.Error($"JobID({PlaceID})({this.Username}) - {e.Message}"); }

					if (Launcher == null || Launcher.HasExited)
					{
						Logger.Error($"JobID({PlaceID}) is failed to start in {DateTime.Now} ({this.Username})");
						return;
					}

					Process pid = await Wait4Roblox(Ticket);
					if (pid == null) throw new Exception($"Something wrong with this roblox instance: Bloxstrap cannot launch roblox. {Username}");
					IsActive = pid.Id;
					_Ticket = Ticket;

					Task errorcheck = Task.Run(async () =>
					{
						int i = 0;
						while (++i <= AccountManager.General.Get<int>("LaunchDelay") / 2)
						{
							await Task.Delay(TimeSpan.FromSeconds(2));
							pid.Refresh();
							if (pid.MainWindowTitle == "Roblox")
							{
								await Task.Delay(TimeSpan.FromSeconds(10));
								break;
							}
							if (pid.MainWindowTitle == "Authentication Failed") break;
						}
						if (pid.MainWindowTitle != "Roblox") throw new Exception($"Something wrong with this roblox instance: ({(pid.MainWindowTitle.IsNullOrEmpty() ? "Window, doesnt open" : pid.MainWindowTitle)}), {this.Username}.");
					});

					ActiveItem ret = new ActiveItem()
					{
						Account = this,
						PID = pid.Id,
						StartTime = DateTime.Now
					};
					job.ProcessList.AddOrChange(ret);
					AccountManager.AllRunningAccounts.AddOrChange(ret);
					Logger.Information($"JobID({PlaceID}) is successfuly started in {DateTime.Now} ({this.Username})");
					await errorcheck;

					try { semaphore.Release(); } catch { }
					PS.EndProcess();
					Launcher.WaitForExit();
				}
				catch (Exception x)
				{
					PS.EndProcess();
					if (Launcher != null)
					{
						AccountManager.AllRunningAccounts.RemoveAll(item => item.PID == Launcher.Id);
						job.ProcessList.RemoveAll(item => item.PID == Launcher.Id);
						this.LeaveServer();
					}
					try { semaphore.Release(); } catch { }
					this.IsActive = 0;
					Logger.Error($"Error: {x.Message}", x);
				}
			});
		}

		private async void LoginFailedProcedure(string text)
		{
			try { semaphore.Release(); } catch { }
			Logger.Error(text);
			PS.EndProcess();
			IsActive = 0;
		}

		private bool CheckTicket(string command, string ticket) => command.Contains(ticket);

		public void LeaveServer(long jid = 0)
		{
			int process = this.IsActive;
			//if (jid != 0)
			//	AccountManager.ActiveJobs.Find(job => job.Jid == jid).ProcessList.RemoveAll(prcs => prcs.Account.IsActive == process);
			try
			{
				this.IsActive = 0;
				if (process != 0)
				{
					var proc = Process.GetProcessById(process);
					proc.CloseMainWindow();
					proc.Kill();
				}
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to kill process {process}", e);
			}
			AccountManager.AllRunningAccounts.RemoveAll(item => item.PID == process);
		}

		public void LogOutAcc()
		{
			//UpdateCell(new List<ValueRange>{
			//	CreateAccountsTableRange($"{Columns["Status"]}{Row}", string.Empty).GetAwaiter().GetResult(),
			//	CreateAccountsTableRange($"{Columns["State"]}{Row}", "Standby").GetAwaiter().GetResult()
			//}).GetAwaiter().GetResult();
		}

		public async void AdjustWindowPosition()
		{
			if (!RobloxWatcher.RememberWindowPositions)
				return;

			if (!(int.TryParse(GetField("Window_Position_X"), out int PosX) && int.TryParse(GetField("Window_Position_Y"), out int PosY) && int.TryParse(GetField("Window_Width"), out int Width) && int.TryParse(GetField("Window_Height"), out int Height)))
				return;

			bool Found = false;
			DateTime Ends = DateTime.Now.AddSeconds(45);

			while (true)
			{
				await Task.Delay(350);

				foreach (var process in Process.GetProcessesByName("RobloxPlayerBeta").Reverse())
				{
					if (process.MainWindowHandle == IntPtr.Zero) continue;

					string CommandLine = process.GetCommandLine();

					var TrackerMatch = Regex.Match(CommandLine, @"\-b (\d+)");
					string TrackerID = TrackerMatch.Success ? TrackerMatch.Groups[1].Value : string.Empty;

					if (TrackerID != BrowserTrackerID) continue;

					Found = true;

					MoveWindow(process.MainWindowHandle, PosX, PosY, Width, Height, true);

					break;
				}

				if (Found) break;

				if (DateTime.Now > Ends) break;
			}
		}

		public string GetField(string Name) => Fields.ContainsKey(Name) ? Fields[Name] : string.Empty;
		public void SetField(string Name, string Value) { Fields[Name] = Value; /*AccountManager.SaveAccounts();*/ }
	}
}