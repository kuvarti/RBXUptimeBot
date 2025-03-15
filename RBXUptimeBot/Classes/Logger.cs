using log4net;
using log4net.Appender;
using log4net.Core;
using System.Collections.Concurrent;
using System.Text.Json;
using RBXUptimeBot.Models;
using LogLevel = RBXUptimeBot.Models.LogLevel;
using RBXUptimeBot.Models.Entities;

namespace RBXUptimeBot.Classes
{
	public static class Logger
	{
		// Define a thread-safe collection to store log entries
		private static readonly ConcurrentQueue<LogTableEntity> _logEntries = new ConcurrentQueue<LogTableEntity>();

		// Method to log a message with a specific level
		public static LogTableEntity Log(LogLevel level, string message, Exception? exception = null)
		{
			var logEntry = new LogTableEntity
			{
				Timestamp = DateTime.UtcNow,
				Level = Convert.ToInt16(level),
				Message = message,
				Exception = exception?.ToSerializable()
			};
			_logEntries.Enqueue(logEntry);
			AccountManager.postgreService.LogTable.Add(logEntry);
			AccountManager.postgreService.SaveChangesAsync();
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

	public static class ExceptionExtensions
	{
		public static SerializableException ToSerializable(this Exception ex)
		{
			if (ex == null) return null;

			return new SerializableException
			{
				Message = ex.Message,
				StackTrace = ex.StackTrace,
				Source = ex.Source,
				TargetSite = ex.TargetSite?.ToString(),
				ExceptionType = ex.GetType().FullName,
				InnerException = ex.InnerException?.ToSerializable(),
				Data = ex.Data != null && ex.Data.Count > 0 ? new Dictionary<string, object>() : null
			};
		}
	}
}
