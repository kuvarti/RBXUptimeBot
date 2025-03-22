using log4net;
using log4net.Appender;
using log4net.Core;
using System.Collections.Concurrent;
using System.Text.Json;
using RBXUptimeBot.Models;
using LogLevel = RBXUptimeBot.Models.LogLevel;
using RBXUptimeBot.Models.Entities;
using WebSocketSharp;
using RBXUptimeBot.Classes.Services;
using Microsoft.EntityFrameworkCore;

namespace RBXUptimeBot.Classes
{
	public static class Logger
	{
		// Define a thread-safe collection to store log entries
		private static readonly ConcurrentQueue<LogTableEntity> _logEntries = new ConcurrentQueue<LogTableEntity>();

		// Method to log a message with a specific level
		public static LogTableEntity Log(LogLevel level, string message, Exception? exception = null, bool savePostgre = true)
		{
			JsonDocument exceptionMessage = null;

			try {
				if (exception != null) exceptionMessage = ExceptionHelper.ExceptionToJsonDocument(exception);
			} catch (Exception e){
				exceptionMessage = JsonDocument.Parse(JsonSerializer.Serialize($"{e.Message}. -- {exception.Message}"));
			}

			var logEntry = new LogTableEntity
			{
				Timestamp = DateTime.UtcNow,
				Level = Convert.ToInt16(level),
				Message = message,
				Exception = exceptionMessage
			};
			_logEntries.Enqueue(logEntry);
			if (!savePostgre) return logEntry;

			using (var postgre = new PostgreService<LogTableEntity>(new DbContextOptionsBuilder<PostgreService<LogTableEntity>>().UseNpgsql(AccountManager.ConnStr).Options))
			{
				postgre.Table?.Add(logEntry);
				postgre.SaveChanges();
			}
			return logEntry;
		}

		// Convenience methods for different log levels
		public static LogTableEntity Trace(string message) => Log(LogLevel.Trace, message);
		public static LogTableEntity Debug(string message) => Log(LogLevel.Debug, message);
		public static LogTableEntity Information(string message) => Log(LogLevel.Information, message);
		public static LogTableEntity Warning(string message) => Log(LogLevel.Warning, message);
		public static LogTableEntity Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
		public static LogTableEntity Critical(string message, Exception? exception = null) => Log(LogLevel.Critical, message, exception);

		// Method to retrieve all logs
		public static IEnumerable<LogTableEntity> GetAllLogs()
		{
			return _logEntries.ToArray();
		}

		// Optional: Method to clear logs
		public static void ClearLogs()
		{
			while (_logEntries.TryDequeue(out _)) { }
		}
	}

	public static class ExceptionHelper
	{
		public static JsonDocument ExceptionToJsonDocument(Exception ex)
		{
			// Exception detaylarını anonim bir nesneye aktaralım.
			var exceptionDetails = new
			{
				ExceptionType = ex.GetType().FullName,
				Message = ex.Message,
				StackTrace = ex.StackTrace,
				InnerException = ex.InnerException != null ? new
				{
					ExceptionType = ex.InnerException.GetType().FullName,
					Message = ex.InnerException.Message,
					StackTrace = ex.InnerException.StackTrace
				} : null
			};

			// Anonim nesneyi JSON string'e dönüştürelim.
			string jsonString = JsonSerializer.Serialize(exceptionDetails);

			// JSON string'i JsonDocument'e parse edelim.
			return JsonDocument.Parse(jsonString);
		}
	}
}
