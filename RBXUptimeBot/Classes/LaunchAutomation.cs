using System;
using System.Diagnostics;

namespace RBXUptimeBot.Classes
{
	public class LaunchAutomation
	{
		private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
		private Process proxyfier;
		public LaunchAutomation() { }

		private string GetRandomProxy()
		{
			string[] files = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Proxies"));
			if (files.Length <= 0)
			{
				AccountManager.LogService.CreateAsync(Logger.Error("There is no proxy profile."));
				return null;
			}
			Random random = new Random();
			int randomIndex = random.Next(files.Length);

			return Path.GetFullPath(files[randomIndex]);
		}

		public void Lock() => semaphore.WaitAsync();
		public void Unlock() => semaphore.Release();

		public void LaunchProcess()
		{
			semaphore.WaitAsync();
			string profilePath = GetRandomProxy();
			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = "C:\\Program Files (x86)\\Proxifier\\Proxifier.exe",
				Arguments = $"\"{profilePath}\" silent-load",
				CreateNoWindow = true,
				UseShellExecute = false
			};
			try
			{
				proxyfier = Process.Start(psi);
			}
			catch (Exception ex)
			{
				AccountManager.LogService.CreateAsync(Logger.Error($"An error accoured while launching proxfier : {ex.Message}", ex));
			}
		}

		public void EndProcess()
		{
			if (proxyfier != null)
				proxyfier.Kill();
			try { Unlock(); } catch { }
		}
	}
}
