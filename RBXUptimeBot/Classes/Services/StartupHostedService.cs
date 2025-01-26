using RBXUptimeBot.Models;

namespace RBXUptimeBot.Classes.Services
{
	public class StartupHostedService : IHostedService
	{
		private readonly IMongoDbService<LogEntry> _logService;

		public StartupHostedService(IMongoDbService<LogEntry> logService)
		{
			_logService = logService;
		}

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			// Uygulama başlarken yapılacak işlemler
			var yeniLog = new LogEntry
			{
				Message = "Uygulama Başlatıldı",
				Timestamp = DateTime.UtcNow
			};

			await _logService.CreateAsync(yeniLog);

			Console.WriteLine("StartupHostedService: LogEntry oluşturuldu.");
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			// Uygulama kapanırken yapılacak işlemler (varsa)
			return Task.CompletedTask;
		}
	}
}
