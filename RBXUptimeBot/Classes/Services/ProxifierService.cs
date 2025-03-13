using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Diagnostics;
using System.Xml;

namespace RBXUptimeBot.Classes.Services
{
	public class Proxy {
		public string ProxyId { get; init; }
		public string ProxyIP { get; init; }
		public string ProxyPort { get; init; }
		public string ProxyUsername { get; init; }
		public string ProxyPassword { get; init; }
	}
	public class ProxifierService
	{
		// Senkronizasyon için lock nesnesi
		private static readonly string ProxyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Proxies");
		private static readonly string TempProxyFile = "ProxyFile.ppx";
		private static List<Proxy> Proxies = new List<Proxy>();
		private static readonly object lockObj = new object();
		static readonly Dictionary<string, int> Columns = new Dictionary<string, int> {
			{"ID", 1},
			{"IP", 2},
			{"Port", 3},
			{"Username", 4},
			{"Password", 5}
		};

		private Proxy _Proxy;
		public Proxy Proxy { get => _Proxy; }
		private static Process proxifierProcess;

		public ProxifierService(string _proxyId) {
			_Proxy = Proxies.Find(p => p.ProxyId == _proxyId);
			if (_Proxy == null)
			{
				_Proxy = new Proxy() { ProxyId = _proxyId};
			}
		}

		private bool RefreshProxy()
		{
			if (!IsValid) {
				string id = _Proxy.ProxyId;
				_Proxy = Proxies.Find(p => p.ProxyId == id);
				if (_Proxy == null)
				{
					_Proxy = new Proxy() { ProxyId = id };
					return false;
				}
			}
			return true;
		}

		// Proxifier'ın çalışıp çalışmadığını kontrol eden property.
		public bool IsValid => _Proxy != null && _Proxy.ProxyIP != null;
		public bool IsRunning => proxifierProcess != null && !proxifierProcess.HasExited;

		// Proxy Profile File Operations
		private bool CreateProxyProfile() {
			string templateFile = Path.Combine(ProxyPath, "Template.ppx");
			XmlDocument xmlDoc = new XmlDocument();
			xmlDoc.Load(templateFile);
			XmlNode node;

			if (!IsValid) return false;
			DeleteProxyProfile();

			node = xmlDoc.SelectSingleNode("//Username");
			if (node != null) node.InnerText = _Proxy.ProxyUsername;

			node = xmlDoc.SelectSingleNode("//Password");
			if (node != null) node.InnerText = _Proxy.ProxyPassword;

			node = xmlDoc.SelectSingleNode("//Address");
			if (node != null) node.InnerText = _Proxy.ProxyIP;

			node = xmlDoc.SelectSingleNode("//Port");
			if (node != null) node.InnerText = _Proxy.ProxyPort;

			xmlDoc.Save(Path.Combine(ProxyPath, TempProxyFile));
			return true;
		}
		private bool DeleteProxyProfile() {
			string s = Path.Combine(ProxyPath, TempProxyFile);
			if (File.Exists(s))
			{
				File.Delete(s);
			}
			return false;
		}

		public void LaunchProcess()
		{
			Process processToWait = null;
			// İlk olarak, lock içerisinde kontrol ediyoruz.
			lock (lockObj)
			{
				if (IsRunning)
				{
					// Mevcut çalışan process referansını alıyoruz.
					processToWait = proxifierProcess;
				}
			}

			// Eğer proxifier çalışıyorsa, lock dışında bekleyerek mevcut process'in bitmesini sağlıyoruz.
			if (processToWait != null)
			{
				try
				{
					processToWait.WaitForExit();
				}
				catch (Exception ex)
				{
					Logger.Error($"Error while waiting for proxifier to exit: {ex.Message}", ex);
					return;
				}
			}

			// Tekrar lock alarak, yeni proxifier başlatma işlemini gerçekleştiriyoruz.
			lock (lockObj)
			{
				// Bu kontrol, bekleme sırasında başka bir thread'in başlatıp başlatmadığını yakalar.
				if (IsRunning || !RefreshProxy())
				{
					Logger.Information("Proxifier is already running or proxy ID cannot found.");
					return;
				}

				if (!CreateProxyProfile())
					return;

				// Dinamik proxifier exe yolunu konfigürasyondan alıyoruz.
				string proxifierPath = AccountManager.General.Get<string>("Proxifier-Path");
				if (string.IsNullOrEmpty(proxifierPath) || !File.Exists(proxifierPath))
				{
					Logger.Error("Proxifier path is invalid.");
					return;
				}

				ProcessStartInfo psi = new ProcessStartInfo
				{
					FileName = proxifierPath,
					Arguments = $"\"{Path.Combine(ProxyPath, TempProxyFile)}\" silent-load",
					CreateNoWindow = true,
					UseShellExecute = false
				};

				try
				{
					proxifierProcess = Process.Start(psi);
					Task.Delay(TimeSpan.FromSeconds(1));
				}
				catch (Exception ex)
				{
					Logger.Error($"Error while launching proxifier: {ex.Message}", ex);
				}
			}
		}

		// Proxifier'ı sonlandırma
		public void EndProcess()
		{
			lock (lockObj)
			{
				try
				{
					if (proxifierProcess != null && !proxifierProcess.HasExited)
					{
						proxifierProcess.Kill();
						proxifierProcess = null;
						DeleteProxyProfile();
					}
				}
				catch (Exception ex)
				{
					Logger.Error($"Error while ending proxifier process: {ex.Message}", ex);
				}
			}
		}

		public static void EndProxifiers()
		{
			var pl = Process.GetProcessesByName("proxifier");
			foreach (var item in pl)
			{
				item.Kill();
			}
		}
		
		// Keep ProxyList Updated
		public static async Task LoadProxyList()
		{

			while (true) {
				try
				{
					var response = AccountManager.postgreService.ProxyTable.ToList();
					foreach (var item in response)
					{
						var exist = Proxies.Find(p => p.ProxyId == item.ProxyName);
						Proxy tmp = new Proxy()
						{
							ProxyId = item.ProxyName,
							ProxyIP = item.ProxyIP,
							ProxyPort = item.ProxyPort,
							ProxyUsername = item.ProxyUser,
							ProxyPassword = item.ProxyPassword
						};
						if (exist == null) Proxies.Add(tmp);
						else if (exist != tmp)
						{
							Proxies.Remove(exist);
							Proxies.Add(tmp);
						}
					}
				}
				catch (Exception ex) { }
				Task.Delay(TimeSpan.FromMinutes(10));
			}
		}
	}
}
