using Microsoft.EntityFrameworkCore;
using RBXUptimeBot.Models.Entities;
using System;
using System.Diagnostics;
using System.Net;
using System.Xml;

namespace RBXUptimeBot.Classes.Services
{
	public class Proxy {
		public int ProxyId { get; init; }
		public string ProxyName { get; init; }
		public string ProxyIP { get; init; }
		public string ProxyPort { get; init; }
		public string ProxyUsername { get; init; }
		public string ProxyPassword { get; init; }
	}
	public class ProxifierService
	{
		private static readonly string ProxyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Proxies");
		private static readonly string TempProxyFile = "ProxyFile.ppx";
		private static List<Proxy> Proxies = new List<Proxy>();
		private static Process proxifierProcess;

		private Proxy _Proxy;
		public Proxy Proxy { get => _Proxy; }

		public ProxifierService(int _proxyId) {
			_Proxy = Proxies.Find(p => p.ProxyId == _proxyId);
			if (_Proxy == null)
			{
				_Proxy = new Proxy() { ProxyId = _proxyId};
			}
		}

		private bool RefreshProxy()
		{
			if (!IsValid) {
				int id = _Proxy.ProxyId;
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
		private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
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

		public async Task LaunchProcess()
		{
			Process processToWait = null;

			// İlk olarak, semaphore içerisinde kontrol ediyoruz.
			await semaphore.WaitAsync();
			try
			{
				if (IsRunning)
				{
					// Mevcut çalışan process referansını alıyoruz.
					processToWait = proxifierProcess;
				}
			}
			finally
			{
				semaphore.Release();
			}

			// Eğer proxifier çalışıyorsa, semaphore dışında bekleyerek mevcut process'in bitmesini sağlıyoruz.
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

			// Tekrar semaphore alarak, yeni proxifier başlatma işlemini gerçekleştiriyoruz.
			await semaphore.WaitAsync();
			try
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
				}
				catch (Exception ex)
				{
					Logger.Error($"Error while launching proxifier: {ex.Message}", ex);
				}
			}
			finally
			{
				semaphore.Release();
			}
		}

		// Proxifier'ı sonlandırma
		// Proxifier'ı sonlandırma
		public async Task EndProcess()
		{
			await semaphore.WaitAsync();
			try
			{
				if (proxifierProcess != null && !proxifierProcess.HasExited)
				{
					proxifierProcess.Kill();
					proxifierProcess.Dispose();
					proxifierProcess = null;
					DeleteProxyProfile();
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"Error while ending proxifier process: {ex.Message}", ex);
			}
			finally
			{
				semaphore.Release();
			}
		}

		public async Task WaitInitialize()
		{
			int i = 0, max = AccountManager.General.Get<int>("ProxifierTimeout");
			while (++i <= max) {
				HttpClient _httpClient = new HttpClient();
				if (await _httpClient.GetStringAsync("https://ifconfig.me/ip") == Proxy.ProxyIP){
					_httpClient.Dispose();
					return;
				}
				_httpClient.Dispose();
				await Task.Delay(TimeSpan.FromSeconds(3));
			}
			await EndProcess();
			throw new Exception($"Proxifier initialization timeout.");
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
					using (var postgre = new PostgreService<ProxyTableEntity>(new DbContextOptionsBuilder<PostgreService<ProxyTableEntity>>().UseNpgsql(AccountManager.ConnStr).Options))
					{
						var response = await postgre.Table?.ToListAsync();
						if (response == null) continue;
						foreach (var item in response)
						{
							var exist = Proxies.Find(p => p.ProxyId == item.ID);
							Proxy tmp = new Proxy()
							{
								ProxyId = item.ID,
								ProxyIP = item.ProxyIP,
								ProxyPort = item.ProxyPort,
								ProxyName = item.ProxyName,
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
				}
				catch (Exception ex) { }
				await Task.Delay(TimeSpan.FromMinutes(10));
			}
		}
	}
}
