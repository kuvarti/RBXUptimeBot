using CefSharp;
using CefSharp.WinForms;
using Newtonsoft.Json.Linq;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Numerics;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebSocketSharp;
using Yove.Proxy;
using System.Drawing;
using PuppeteerSharp.Helpers;
using Microsoft.AspNetCore.Http.HttpResults;

namespace RBXUptimeBot.Classes
{
	internal class AccountBrowser
	{
		public static BrowserFetcher Fetcher = new BrowserFetcher(Product.Chrome);

		//private static Dictionary<int, Vector2> ScreenGrid;
		public class LoginResult
		{
			public void Ok(string _)
			{
				Success = true;
				Message = _;
			}
			public void Fail(string _)
			{
				Success = false;
				Message = _;
			}

			public bool Success { get; set; }
			public string Message { get; set; }
		}

		private readonly Dictionary<string, string> Images = new Dictionary<string, string>();
		private readonly HashSet<string> Solved = new HashSet<string>();
		private ProxyClient Proxy = null;
		private string Password;

		public Browser browser;
		public Page page;
		public Vector2 Size = new Vector2(880, 740), Position;

		public AccountBrowser() { }

		public AccountBrowser(Account account, string Url = null, string Script = null, Func<Page, Task> PostNavigation = null)
		{
			_ = LaunchBrowser(Url ?? string.Empty, Script: Script, PostNavigation: PostNavigation, PostPageCreation: () => page.SetCookieAsync(new CookieParam
			{
				Name = ".ROBLOSECURITY",
				Domain = ".roblox.com",
				Expires = (DateTime.Now.AddYears(1) - DateTime.MinValue).TotalSeconds,
				HttpOnly = true,
				Secure = true,
				Url = "https://roblox.com",
				Value = account.SecurityToken
			}));
		}

		public async Task LaunchBrowser(string Url = "https://roblox.com/", Func<Task> PostPageCreation = null, Func<Page, Task> PostNavigation = null, string Script = null, string[] Arguments = null)
		{
			if (string.IsNullOrEmpty(Url)) Url = "https://roblox.com/";

			Position = new Vector2(100, 100);

			List<string> Args = new List<string>(Arguments ?? new string[] { "--disable-web-security" });

			string ExtensionPath = Path.Combine(Environment.CurrentDirectory, "extension");
			string ConfigPath = Path.Combine(Environment.CurrentDirectory, "BrowserConfig.json");
			BrowserConfig Config = null;

			if (Directory.Exists(ExtensionPath))
				Args.AddRange(new string[] { $@"--disable-extensions-except=""{ExtensionPath}""", $@"--load-extension=""{ExtensionPath}""" });
			if (File.Exists(ConfigPath))
				File.ReadAllText(ConfigPath).TryParseJson(out Config);

			if (Config?.CustomArguments != null) Args.AddRange(Config.CustomArguments);

			if (Arguments == null)
				Args.AddRange(new string[] { $"--window-size=\"{(int)Size.X},{(int)Size.Y}\"", $"--window-position=\"{(int)Position.X},{(int)Position.Y}\"" });

			string ProxiesPath = Path.Combine(Environment.CurrentDirectory, "proxies.txt");
			string ProxyString = string.Empty, Username = string.Empty, Password = string.Empty;
			string proxyError = "[";

			if (AccountManager.General.Get<bool>("UseProxies") && File.Exists(ProxiesPath))
			{ // Format: [optional protocol://]ip:port@user:pass / socks5://1.2.3.4:999@user:pass
				Random Rng = new Random();
				int Timeout = AccountManager.General.Exists("ProxyTimeout") ? AccountManager.General.Get<int>("ProxyTimeout") : 3000;
				int Limit = AccountManager.General.Exists("ProxyTestLimit") ? AccountManager.General.Get<int>("ProxyTestLimit") : 10;
				List<string> Proxies = File.ReadAllLines(ProxiesPath).ToList();
				Proxies.RemoveAll(Proxies => Proxies.StartsWith('#') || Proxies.Equals(""));

				Proxies.OrderBy(x => Rng.Next());

				for (int i = 0; i < Limit; i++)
				{
					if (i > Proxies.Count - 1) break;

					ProxyString = Proxies[i];

					string _Proxy = ProxyString;

					if (_Proxy.Contains("://"))
						_Proxy = _Proxy.Substring(_Proxy.IndexOf("://") + 3);

					Uri ProxyUrl = new Uri($"http://{(_Proxy.Contains("@") ? _Proxy.Substring(0, _Proxy.IndexOf('@')) : _Proxy)}");

					if (ProxyString.Contains("@") && ProxyString.Substring(ProxyString.IndexOf('@')).Contains(':'))
					{
						string Combo = ProxyString.Substring(ProxyString.IndexOf('@') + 1);

						ProxyString = ProxyString.Substring(0, ProxyString.IndexOf('@'));
						Username = Combo.Substring(0, Combo.IndexOf(':'));
						Password = Combo.Substring(Combo.IndexOf(':') + 1);
					}

					ProxyType Protocol = ProxyType.Http;

					if (ProxyString.StartsWith("socks5://"))
						Protocol = ProxyType.Socks5;
					else if (ProxyString.StartsWith("socks4://"))
						Protocol = ProxyType.Socks4;

					Proxy = new ProxyClient(ProxyUrl.Host, ProxyUrl.Port, Username, Password, Protocol);

					using (var Handler = new HttpClientHandler() { Proxy = Proxy })
					using (var Client = new HttpClient(Handler) { Timeout = TimeSpan.FromMilliseconds(Timeout) })
						try { (await Client.GetAsync("https://auth.roblox.com/")).EnsureSuccessStatusCode(); }
						catch (Exception e)
						{
							if (proxyError.Length > 1) proxyError += ",";
							ProxyString = string.Empty;
							proxyError += $"{{\"Message\":\"{e.Message.Replace('\"', '$')}\"}}";
						}

					if (!string.IsNullOrEmpty(ProxyString)) break;
				}

				proxyError += "]";
				if (!string.IsNullOrEmpty(ProxyString))
				{
					ProxyString = Proxy?.GetProxy(null).Authority ?? ProxyString;

					if (ProxyString.StartsWith("http://") || ProxyString.StartsWith("https://"))
						ProxyString = ProxyString.Substring(ProxyString.IndexOf("://") + 3);

					Args.Add($"--proxy-server={ProxyString}");
				}
				else await AccountManager.LogService.CreateAsync(Logger.Error($"No Proxies found or logged in while account login. Process will be continue without proxy.", new Exception(proxyError)));
			}

			var Options = new LaunchOptions { Headless = false, DefaultViewport = null, Args = Args.ToArray(), IgnoreHTTPSErrors = true };

			await Fetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision);

			browser = (Browser)await new PuppeteerExtra().Use(new StealthPlugin()).LaunchAsync(Options);
			page = (Page)(await browser.PagesAsync())[0];

			if (Proxy != null) browser.Disconnected += (s, e) => Proxy.Dispose();

			await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36");

			if (Config?.PreNavigateActions != null)
				foreach (var action in Config.PreNavigateActions)
				{
					if (!Uri.IsWellFormedUriString(action.Key, UriKind.Absolute)) continue;

					await page.GoToAsync(action.Key);
					await page.EvaluateExpressionAsync(action.Value);
				}

			if (Config?.PreNavigateScripts != null)
				foreach (var script in Config.PreNavigateScripts)
					await page.EvaluateExpressionAsync(script);

			if (PostPageCreation != null) try { await PostPageCreation(); } catch { }

			try { await page.GoToAsync(Url, new NavigationOptions { Referer = "https://google.com/", Timeout = 300000 }); } catch { }

			if (!string.IsNullOrEmpty(Script)) await page.EvaluateExpressionAsync(Script);

			//page.FrameAttached += Page_FrameAttached;

			if (PostNavigation != null) try { await PostNavigation(page); } catch { }

			if (Config?.PostNavigateScripts != null)
				foreach (var script in Config.PostNavigateScripts)
					await page.EvaluateExpressionAsync(script);
		}

		public async Task<LoginResult> Login(string Username = "", string Password = "", string[] Arguments = null)
		{
			var ret = new LoginResult { Success = false, Message = "Unknown" };
			await LaunchBrowser("https://roblox.com/login", Arguments: Arguments, PostNavigation: async (page) => await LoginTask(page, ret, Username, Password));
			return ret;
		}

		public async Task LoginTask(Page page, LoginResult result, string Username = "", string Password = "")
		{
			var tcs = new TaskCompletionSource<bool>();

			async void Page_RequestFinished(object sender, RequestEventArgs e)
			{
				try
				{
					Uri Url = new Uri(e.Request.Url);
					if (e.Request.Response.Status == HttpStatusCode.OK && e.Request.Method == HttpMethod.Post && Url.Host == "auth.roblox.com")
					{
						if ((Url.AbsolutePath == "/v2/login" || Url.AbsolutePath == "/v2/signup") && e.Request.PostData != null && Utilities.TryParseJson((string)e.Request.PostData, out JObject LoginData))
						{
							if (LoginData?["password"]?.Value<string>() is string password && !string.IsNullOrEmpty(password) && LoginData?["ctype"].Value<string>() is string loginType && loginType.ToLowerInvariant() == "username")
								Password = password;
							if ((await page.GetCookiesAsync("https://roblox.com/")).FirstOrDefault(Cookie => Cookie.Name == ".ROBLOSECURITY") is CookieParam Cookie)
							{ result.Ok(Cookie.Value); await browser.DisposeAsync(); }
						}
						else if (Regex.IsMatch(Url.AbsolutePath, "/users/[0-9]+/two-step-verification/login") && (await page.GetCookiesAsync("https://roblox.com/")).FirstOrDefault(Cookie => Cookie.Name == ".ROBLOSECURITY") is CookieParam Cookie)
						{ result.Ok(Cookie.Value); await browser.DisposeAsync(); }
						else {
							result.Fail($"Account {Username} cannot be logged in.");
							await AccountManager.LogService.CreateAsync(Logger.Warning($"Account {Username} cannot be logged in."));
						}
						tcs.TrySetResult(true);
					}
				}
				catch (Exception ex)
				{
					await AccountManager.LogService.CreateAsync(Logger.Error($"Exception in RequestFinished handler: {ex}", ex));
					tcs.TrySetException(ex);
				}
			}

			page.RequestFinished += Page_RequestFinished;

			await page.EvaluateExpressionAsync(@"document.body.classList.remove(""light-theme"");document.body.classList.add(""dark-theme"");");

			try
			{
				if (!string.IsNullOrEmpty(Username) && await page.WaitForSelectorAsync("#login-username", new WaitForSelectorOptions() { Timeout = 5000 }) != null)
					await page.TypeAsync("#login-username", Username);
				if (!string.IsNullOrEmpty(Password) && await page.WaitForSelectorAsync("#login-password", new WaitForSelectorOptions() { Timeout = 5000 }) != null)
					await page.TypeAsync("#login-password", Password);

				if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password)) await page.ClickAsync("#login-button");

				int active = 0, max = AccountManager.General.Get<int>("CaptchaTimeOut");
				while (!page.IsClosed)
				{
					if (active >= max)
					{
						result.Fail($"Account {Username} login attempt timeout. (5 min)");
						await AccountManager.LogService.CreateAsync(Logger.Error($"Account {Username} login attempt timeout. (5 min)"));
						break;
					};
					if (page.Url == "https://www.roblox.com/login/securityNotification")
					{
						result.Fail($"Account {Username} needs email for login.");
						await AccountManager.LogService.CreateAsync(Logger.Warning($"Account {Username} needs email for login."));
						break;
					}
					await Task.Delay(TimeSpan.FromSeconds(2));
					active += 2;
				}
				if (!page.IsClosed) page.CloseAsync();
			}
			catch (Exception ex)
			{
				await AccountManager.LogService.CreateAsync(Logger.Error($"An exception was caught while trying to automatically log in: {ex.Message}", ex));
			}
			// here is the wait for Page_RequestFinished function to be waited
			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
			{
				try
				{
					await tcs.Task.WaitAsync(cts.Token);
				}
				catch (OperationCanceledException a)
				{
					await AccountManager.LogService.CreateAsync(Logger.Error("Waiting for RequestFinished timed out.", a));
				}
				finally
				{
					page.RequestFinished -= Page_RequestFinished;
				}
			}
		}
	}

	internal class CefBrowser
	{
		private static CefBrowser BrowserForm;
		public static CefBrowser Instance => BrowserForm ??= new CefBrowser();

		private Browser _browser;
		private Page _page;
		private bool BrowserMode = false;
		private string Password;

		private CefBrowser() { InitializeAsync(); }

		public async Task InitializeAsync()
		{
			if (!Directory.Exists(Path.Combine(Environment.CurrentDirectory, "puppeteer")))
			{
				Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "puppeteer"));
			}

			await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
			_browser = (Browser)await Puppeteer.LaunchAsync(new LaunchOptions
			{
				Headless = false, // Tarayıcı arayüzü gösterilir
				Args = new[] { "--start-maximized" }
			});
		}

		public async Task Login()
		{
			if (_browser == null) throw new InvalidOperationException("Browser is not initialized.");

			BrowserMode = false;
			_page = (Page)await _browser.NewPageAsync();
			await _page.GoToAsync("https://roblox.com/login");
		}

		public async Task EnterBrowserMode(string securityToken, string url = "https://roblox.com/home")
		{
			if (_browser == null) throw new InvalidOperationException("Browser is not initialized.");

			BrowserMode = true;

			// Yeni bir sayfa aç
			_page = (Page)await _browser.NewPageAsync();

			// Çerez ayarı
			await _page.SetCookieAsync(new CookieParam
			{
				Name = ".ROBLOSECURITY",
				Value = securityToken,
				Domain = ".roblox.com",
				Expires = (DateTime.Now.AddYears(1) - DateTime.MinValue).TotalSeconds,
				HttpOnly = true,
				Secure = true
			});

			// Belirtilen URL'ye git
			await _page.GoToAsync(url);
		}

		private async Task OnNavigated()
		{
			if (_page == null) return;

			var url = _page.Url;
			Console.WriteLine($"Browser Navigated to {url}");

			if (url.Contains("/home") && !BrowserMode)
			{
				var cookies = await _page.GetCookiesAsync();
				var rSecCookie = Array.Find(cookies, c => c.Name == ".ROBLOSECURITY");

				if (rSecCookie != null)
				{
					//AccountManager.AddAccount(rSecCookie.Value, Password);
					await _page.DeleteCookieAsync(rSecCookie);
					Console.WriteLine("Logged in and cookie cleared.");
				}

				Password = string.Empty;
			}
		}

		private async Task OnPageLoaded()
		{
			if (_page == null) return;

			// Sayfa tamamen yüklendiğinde işlem yap
			await _page.WaitForSelectorAsync("body");

			// Şifre alanını kontrol et
			while (BrowserMode && _page.Url.Contains("login"))
			{
				var passwordElement = await _page.QuerySelectorAsync("#login-password")
									  ?? await _page.QuerySelectorAsync("#signup-password");

				if (passwordElement != null)
				{
					Password = await _page.EvaluateFunctionAsync<string>("el => el.value", passwordElement);
					Console.WriteLine($"Password captured: {Password}");
				}

				await Task.Delay(100); // Tarayıcıya yükleme süresi tanı
			}

			// Sayfa teması değiştir
			await _page.EvaluateExpressionAsync(@"document.body.classList.remove('light-theme');
                                              document.body.classList.add('dark-theme');");
		}

		public async Task CloseBrowserAsync()
		{
			if (_browser != null)
			{
				await _browser.CloseAsync();
				_browser = null;
			}
		}
	}

	internal class BrowserConfig
	{
		public Dictionary<string, string> PreNavigateActions;

		public List<string> PreNavigateScripts;
		public List<string> PostNavigateScripts;

		public List<string> CustomArguments;
	}
}