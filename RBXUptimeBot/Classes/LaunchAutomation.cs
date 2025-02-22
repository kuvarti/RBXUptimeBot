using System;
using System.Diagnostics;

namespace RBXUptimeBot.Classes
{
	public class LaunchAutomation
	{
		// Senkronizasyon için lock nesnesi
		private static readonly object lockObj = new object();
		private Process proxifierProcess;

		// Proxifier'ın çalışıp çalışmadığını kontrol eden property.
		public bool IsRunning
		{
			get { return proxifierProcess != null && !proxifierProcess.HasExited; }
		}

		// Proxies klasöründen rastgele bir profil seçer.
		private string GetRandomProxy()
		{
			string proxiesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Proxies");
			string[] files = Directory.GetFiles(proxiesDirectory);
			if (files.Length <= 0)
			{
				AccountManager.LogService.CreateAsync(Logger.Error("There is no proxy profile."));
				return null;
			}
			Random random = new Random();
			int randomIndex = random.Next(files.Length);
			return Path.GetFullPath(files[randomIndex]);
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
					AccountManager.LogService.CreateAsync(Logger.Error($"Error while waiting for proxifier to exit: {ex.Message}", ex));
					return;
				}
			}

			// Tekrar lock alarak, yeni proxifier başlatma işlemini gerçekleştiriyoruz.
			lock (lockObj)
			{
				// Bu kontrol, bekleme sırasında başka bir thread'in başlatıp başlatmadığını yakalar.
				if (IsRunning)
				{
					AccountManager.LogService.CreateAsync(Logger.Information("Proxifier is already running."));
					return;
				}

				string profilePath = GetRandomProxy();
				if (string.IsNullOrEmpty(profilePath))
					return;

				// Dinamik proxifier exe yolunu konfigürasyondan alıyoruz.
				string proxifierPath = AccountManager.General.Get<string>("Proxifier-Path");
				if (string.IsNullOrEmpty(proxifierPath) || !File.Exists(proxifierPath))
				{
					AccountManager.LogService.CreateAsync(Logger.Error("Proxifier path is invalid."));
					return;
				}

				ProcessStartInfo psi = new ProcessStartInfo
				{
					FileName = proxifierPath,
					Arguments = $"\"{profilePath}\" silent-load",
					CreateNoWindow = true,
					UseShellExecute = false
				};

				try
				{
					proxifierProcess = Process.Start(psi);
				}
				catch (Exception ex)
				{
					AccountManager.LogService.CreateAsync(Logger.Error($"Error while launching proxifier: {ex.Message}", ex));
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
					}
				}
				catch (Exception ex)
				{
					AccountManager.LogService.CreateAsync(Logger.Error($"Error while ending proxifier process: {ex.Message}", ex));
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
	}


}
