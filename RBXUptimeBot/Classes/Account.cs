using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RBXUptimeBot.Classes;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using WebSocketSharp;
using static RBXUptimeBot.Classes.RobloxWatcher;

namespace RBXUptimeBot.Classes
{
	public class Account : IComparable<Account>
	{
		public bool Valid;
		public string SecurityToken;
		public string Username { get; set; }
		public DateTime startTime { get; } //!
		public DateTime LastUse { get; set; }
		public int IsActive { get; set; } = 0;
		private string _Password = "";
		private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string Group { get; set; } = "Default";
		public long UserID;
		public Dictionary<string, string> Fields = new Dictionary<string, string>();
		public DateTime LastAttemptedRefresh;
		[JsonIgnore] public DateTime PinUnlocked;
		[JsonIgnore] public DateTime TokenSet;
		[JsonIgnore] public DateTime LastAppLaunch;
		[JsonIgnore] public string CSRFToken;
		[JsonIgnore] public UserPresence Presence;

		[DllImport("user32.dll", SetLastError = true)]
		static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

		public int CompareTo(Account compareTo)
		{
			if (compareTo == null)
				return 1;
			else
				return Group.CompareTo(compareTo.Group);
		}

		public string BrowserTrackerID;
		public string Password
		{
			get => _Password;
			set
			{
				if (value == null || value.Length > 5000)
					return;

				_Password = value;
				AccountManager.SaveAccounts();
			}
		}

		public Account() { }

		public Account(string Cookie, string AccountJSON = null)
		{
			SecurityToken = Cookie;
			startTime = DateTime.Now;
			AccountJSON ??= AccountManager.MainClient.Execute(MakeRequest("my/account/json", Method.Get)).Content;

			if (!string.IsNullOrEmpty(AccountJSON) && Utilities.TryParseJson(AccountJSON, out AccountJson Data))
			{
				Username = Data.Name;
				UserID = Data.UserId;

				Valid = true;

				LastUse = DateTime.Now;

				AccountManager.LastValidAccount = this;
			}
		}

		public RestRequest MakeRequest(string url, Method method = Method.Get) => new RestRequest(url, method).AddCookie(".ROBLOSECURITY", SecurityToken, "/", ".roblox.com");

		public bool GetAuthTicket(out string Ticket)
		{
			Ticket = string.Empty;

			if (!GetCSRFToken(out string Token)) return false;

			RestRequest request = MakeRequest("v1/authentication-ticket/", Method.Post).AddHeader("X-CSRF-TOKEN", Token).AddHeader("Referer", "https://www.roblox.com/games/4924922222/Brookhaven-RP").AddHeader("Content-type", "application/json"); ;

			RestResponse response = AccountManager.AuthClient.Execute(request);

			Parameter TicketHeader = response.Headers.FirstOrDefault(x => x.Name == "rbx-authentication-ticket");

			if (TicketHeader != null)
			{
				Ticket = (string)TicketHeader.Value;

				return true;
			}

			return false;
		}

		public bool GetCSRFToken(out string Result)
		{
			RestRequest request = MakeRequest("v1/authentication-ticket/", Method.Post).AddHeader("Referer", "https://www.roblox.com/games/4924922222/Brookhaven-RP").AddHeader("Content-type", "application/json");

			RestResponse response = AccountManager.AuthClient.Execute(request);

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
				LastUse = DateTime.Now;

				AccountManager.LastValidAccount = this;
				AccountManager.SaveAccounts();
			}

			CSRFToken = Token;
			TokenSet = DateTime.Now;
			Result = Token;

			return !string.IsNullOrEmpty(Result);
		}

		public bool CheckPin(bool Internal = false)
		{
			if (!GetCSRFToken(out _))
			{
				//if (!Internal) MessageBox.Show("Invalid Account Session!", "Account Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);

				return false;
			}

			if (DateTime.Now < PinUnlocked)
				return true;

			RestRequest request = MakeRequest("v1/account/pin/", Method.Get).AddHeader("Referer", "https://www.roblox.com/");

			RestResponse response = AccountManager.AuthClient.Execute(request);

			if (response.IsSuccessful && response.StatusCode == HttpStatusCode.OK)
			{
				JObject pinInfo = JObject.Parse(response.Content);

				if (!pinInfo["isEnabled"].Value<bool>() || (pinInfo["unlockedUntil"].Type != JTokenType.Null && pinInfo["unlockedUntil"].Value<int>() > 0)) return true;
			}

			//if (!Internal) MessageBox.Show("Pin required!", "Account Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);

			return false;
		}

		public bool UnlockPin(string Pin)
		{
			if (Pin.Length != 4) return false;
			if (CheckPin(true)) return true;

			if (!GetCSRFToken(out string Token)) return false;

			RestRequest request = MakeRequest("v1/account/pin/unlock", Method.Post)
				.AddHeader("Referer", "https://www.roblox.com/")
				.AddHeader("X-CSRF-TOKEN", Token)
				.AddHeader("Content-Type", "application/x-www-form-urlencoded")
				.AddParameter("pin", Pin);

			RestResponse response = AccountManager.AuthClient.Execute(request);

			if (response.IsSuccessful && response.StatusCode == HttpStatusCode.OK)
			{
				JObject pinInfo = JObject.Parse(response.Content);

				if (pinInfo["isEnabled"].Value<bool>() && pinInfo["unlockedUntil"].Value<int>() > 0)
					PinUnlocked = DateTime.Now.AddSeconds(pinInfo["unlockedUntil"].Value<int>());

				if (PinUnlocked > DateTime.Now)
				{
					//MessageBox.Show("Pin unlocked for 5 minutes", "Account Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);

					return true;
				}
			}

			return false;
		}

		public async Task<JToken> GetMobileInfo()
		{
			RestRequest DataRequest = MakeRequest("mobileapi/userinfo", Method.Get);

			RestResponse response = await AccountManager.MainClient.ExecuteAsync(DataRequest);

			if (response.StatusCode == HttpStatusCode.OK && Utilities.TryParseJson(response.Content, out JToken Data))
				return Data;

			return null;
		}

		public bool SetFollowPrivacy(int Privacy)
		{
			if (!CheckPin()) return false;
			if (!GetCSRFToken(out string Token)) return false;

			RestRequest request = MakeRequest("account/settings/follow-me-privacy", Method.Post)
				.AddHeader("Referer", "https://www.roblox.com/my/account")
				.AddHeader("X-CSRF-TOKEN", Token)
				.AddHeader("Content-Type", "application/x-www-form-urlencoded");

			switch (Privacy)
			{
				case 0:
					request.AddParameter("FollowMePrivacy", "All");
					break;
				case 1:
					request.AddParameter("FollowMePrivacy", "Followers");
					break;
				case 2:
					request.AddParameter("FollowMePrivacy", "Following");
					break;
				case 3:
					request.AddParameter("FollowMePrivacy", "Friends");
					break;
				case 4:
					request.AddParameter("FollowMePrivacy", "NoOne");
					break;
			}

			RestResponse response = AccountManager.MainClient.Execute(request);

			if (response.IsSuccessful && response.StatusCode == HttpStatusCode.OK) return true;

			return false;
		}

		public bool ChangeEmail(string Password, string NewEmail)
		{
			if (!CheckPin()) return false;
			if (!GetCSRFToken(out string Token)) return false;

			RestRequest request = MakeRequest("v1/email", Method.Post)
				.AddHeader("Referer", "https://www.roblox.com/")
				.AddHeader("X-CSRF-TOKEN", Token)
				.AddHeader("Content-Type", "application/x-www-form-urlencoded")
				.AddParameter("password", Password)
				.AddParameter("emailAddress", NewEmail);

			RestResponse response = AccountManager.AccountClient.Execute(request);

			if (response.IsSuccessful && response.StatusCode == HttpStatusCode.OK)
				return true;
			return false;
		}

		public bool LogOutOfOtherSessions(bool Internal = false)
		{
			if (!CheckPin(Internal)) return false;
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
					AccountManager.SaveAccounts(true);
				}
				return true;
			}
			return false;
		}

		public RestResponse BlockUserId(string UserID, bool SkipPinCheck = false, HttpListenerContext Context = null, bool Unblock = false)
		{
			if (Context != null) Context.Response.StatusCode = 401;
			if (!SkipPinCheck && !CheckPin(true)) throw new Exception("Pin Locked");
			if (!GetCSRFToken(out string Token)) throw new Exception("Invalid X-CSRF-Token");

			RestRequest blockReq = MakeRequest($"v1/users/{UserID}/{(Unblock ? "unblock" : "block")}", Method.Post).AddHeader("X-CSRF-TOKEN", Token);

			RestResponse blockRes = AccountManager.AccountClient.Execute(blockReq);

			Logger.Information($"Block Response for {UserID} | Unblocking: {Unblock}: [{blockRes.StatusCode}] {blockRes.Content}");

			if (Context != null)
				Context.Response.StatusCode = (int)blockRes.StatusCode;

			return blockRes;
		}

		public RestResponse UnblockUserId(string UserID, bool SkipPinCheck = false, HttpListenerContext Context = null) => BlockUserId(UserID, SkipPinCheck, Context, true);

		public bool UnblockEveryone(out string Response)
		{
			if (!CheckPin(true)) { Response = "Pin is Locked"; return false; }

			RestResponse response = GetBlockedList();

			if (response.IsSuccessful && response.StatusCode == HttpStatusCode.OK)
			{
				Task.Run(async () =>
				{
					JObject List = JObject.Parse(response.Content);

					if (List.ContainsKey("blockedUsers"))
					{
						foreach (var User in List["blockedUsers"])
						{
							if (!UnblockUserId(User["userId"].Value<string>(), true).IsSuccessful)
							{
								await Task.Delay(20000);

								UnblockUserId(User["userId"].Value<string>(), true);

								if (!CheckPin(true))
									break;
							}
						}
					}
				});

				Response = "Unblocking Everyone";

				return true;
			}

			Response = "Failed to unblock everyone";

			return false;
		}

		public RestResponse GetBlockedList(HttpListenerContext Context = null)
		{
			if (Context != null) Context.Response.StatusCode = 401;

			if (!CheckPin(true)) throw new Exception("Pin is Locked");

			RestRequest request = MakeRequest($"v1/users/get-detailed-blocked-users", Method.Get);

			RestResponse response = AccountManager.AccountClient.Execute(request);

			if (Context != null) Context.Response.StatusCode = (int)response.StatusCode;

			return response;
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
				await Task.Delay(TimeSpan.FromSeconds(1));
			}
		}

		private async Task<Process> Wait4Roblox()
		{
			int i = 0, imax = AccountManager.General.Get<int>("BloxstrapTimeout");
			while (true)
			{
				var rbx = Process.GetProcessesByName("RobloxPlayerBeta");
				foreach (var process in rbx)
				{
					if (AccountManager.AllRunningAccounts.Find(acc => acc.PID == process.Id) == null)
						return process;
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
			LaunchAutomation LA = new LaunchAutomation();
			LA.LaunchProcess();
			await Wait4Proxyfier();
			if (string.IsNullOrEmpty(BrowserTrackerID))
			{
				Random r = new Random();
				BrowserTrackerID = r.Next(100000, 175000).ToString() + r.Next(100000, 900000).ToString(); // oh god this is ugly
			}

			if (!GetCSRFToken(out string Token))
			{
				LoginFailedProcedure(LA, $"ERROR: Account {Username} Session Expired, re-add the account or try again. (Invalid X-CSRF-Token)\n{Token}");
				await AccountManager.LogoutAccount(Username);
				return;
			}
			if (AccountManager.ShuffleJobID && string.IsNullOrEmpty(JobID))
				JobID = await Utilities.GetRandomJobId(PlaceID);

			if (!GetAuthTicket(out string Ticket))
			{
				LoginFailedProcedure(LA, "ERROR: Invalid Authentication Ticket, re-add the account or try again\n(Failed to get Authentication Ticket, Roblox has probably signed you out)");
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
				catch (Exception x) { await AccountManager.LogService.CreateAsync(Logger.Error($"An error occured attempting to close {Username}'s last process(es): {x}", x)); }
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
					LoginFailedProcedure(LA, $"JobID({PlaceID}) cannot found while process start ({this.Username}). Process will be aborted");
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
						LaunchInfo.FileName = $"roblox-player:1+launchmode:play+gameinfo:{Ticket}+launchtime:{LaunchTime}+placelauncherurl:{HttpUtility.UrlEncode($"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame{(string.IsNullOrEmpty(JobID) ? "" : "Job")}&browserTrackerId={BrowserTrackerID}&placeId={PlaceID}{(string.IsNullOrEmpty(JobID) ? "" : ("&gameId=" + JobID))}&isPlayTogetherGame=false{(AccountManager.IsTeleport ? "&isTeleport=true" : "")}")}+browsertrackerid:{BrowserTrackerID}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";

					try { Launcher = Process.Start(LaunchInfo); }
					catch (Exception e) { await AccountManager.LogService.CreateAsync(Logger.Error($"JobID({PlaceID})({this.Username}) - {e.Message}")); }

					if (Launcher == null || Launcher.HasExited)
					{
						await AccountManager.LogService.CreateAsync(Logger.Error($"JobID({PlaceID}) is failed to start in {DateTime.Now} ({this.Username})"));
						return;
					}
					Process pid = await Wait4Roblox();
					if (pid == null) {
						throw new Exception($"Something wrong with this roblox instance: Bloxstrap cannot launch roblox. {Username}");
					}
					IsActive = pid.Id;

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
						if (pid.MainWindowTitle != "Roblox")
						{
							throw new Exception($"Something wrong with this roblox instance: ({(pid.MainWindowTitle.IsNullOrEmpty() ? "Window, doesnt open" : pid.MainWindowTitle)}), {this.Username}.");
						}
					});

					var isExist = job.ProcessList.Find(item => item.Account == this);
					if (isExist != null)
					{
						isExist.PID = pid.Id;
						isExist.StartTime = DateTime.Now;
						AccountManager.AllRunningAccounts.Add(isExist);
					}
					else
					{
						ActiveItem ret = new ActiveItem()
						{
							Account = this,
							PID = pid.Id,
							StartTime = DateTime.Now
						};
						job.ProcessList.Add(ret);
						AccountManager.AllRunningAccounts.Add(ret);
					}
					Logger.Information($"JobID({PlaceID}) is successfuly started in {DateTime.Now} ({this.Username})");
					await errorcheck;

					try { semaphore.Release(); } catch { }
					LA.EndProcess();
					Launcher.WaitForExit();
				}
				catch (Exception x)
				{
					LA.EndProcess();
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

		private async void LoginFailedProcedure(LaunchAutomation LA, string text)
		{
			try { semaphore.Release(); } catch { }
			await AccountManager.LogService.CreateAsync(Logger.Error(text));
			LA.EndProcess();
			IsActive = 0;
		}

		public void LeaveServer(long jid = 0)
		{
			int process = this.IsActive;
			//if (jid != 0)
			//	AccountManager.ActiveJobs.Find(job => job.Jid == jid).ProcessList.RemoveAll(prcs => prcs.Account.IsActive == process);
			try
			{
				this.IsActive = 0;
				this.LastUse = DateTime.Now;
				if (process != 0)
				{
					var proc = Process.GetProcessById(process);
					proc.CloseMainWindow();
					proc.Kill();
				}
			}
			catch (Exception e)
			{
				AccountManager.LogService.CreateAsync(Logger.Error($"Failed to kill process {process}", e));
			}
			AccountManager.AllRunningAccounts.RemoveAll(item => item.PID == process);
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

		public string SetServer(long PlaceID, string JobID, out bool Successful)
		{
			Successful = false;

			if (!GetCSRFToken(out string Token)) return $"ERROR: Account Session Expired, re-add the account or try again. (Invalid X-CSRF-Token)\n{Token}";

			if (string.IsNullOrEmpty(Token))
				return "ERROR: Account Session Expired, re-add the account or try again. (Invalid X-CSRF-Token)";

			RestRequest request = MakeRequest("v1/join-game-instance", Method.Post).AddHeader("Content-Type", "application/json").AddJsonBody(new { gameId = JobID, placeId = PlaceID });

			RestResponse response = AccountManager.GameJoinClient.Execute(request);

			if (response.StatusCode == HttpStatusCode.OK)
			{
				Successful = true;
				return Regex.IsMatch(response.Content, "\"joinScriptUrl\":[%s+]?null") ? response.Content : "Success";
			}
			else
				return $"Failed {response.StatusCode}: {response.Content} {response.ErrorMessage}";
		}

		public string GetField(string Name) => Fields.ContainsKey(Name) ? Fields[Name] : string.Empty;
		public void SetField(string Name, string Value) { Fields[Name] = Value; AccountManager.SaveAccounts(); }
		public void RemoveField(string Name) { Fields.Remove(Name); AccountManager.SaveAccounts(); }
	}

	public class AccountJson
	{
		public long UserId { get; set; }
		public string Name { get; set; }
		public string DisplayName { get; set; }
		public string UserEmail { get; set; }
		public bool IsEmailVerified { get; set; }
		public int AgeBracket { get; set; }
		public bool UserAbove13 { get; set; }
	}
}