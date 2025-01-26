using log4net;
using log4net.Appender;
using log4net.Core;
using System.Collections.Concurrent;
using System.Text.Json;
using RBXUptimeBot.Models;
using LogLevel = RBXUptimeBot.Models.LogLevel;

namespace RBXUptimeBot.Classes
{
	public static class Logger
	{
		// Define a thread-safe collection to store log entries
		private static readonly ConcurrentQueue<LogEntry> _logEntries = new ConcurrentQueue<LogEntry>();

		// Method to log a message with a specific level
		public static LogEntry Log(LogLevel level, string message, Exception? exception = null)
		{
			var logEntry = new LogEntry
			{
				Timestamp = DateTime.UtcNow,
				Level = level,
				Message = message,
				Exception = exception?.ToSerializable()
			};
			_logEntries.Enqueue(logEntry);
			return logEntry;
		}

		// Convenience methods for different log levels
		public static LogEntry Trace(string message) => Log(LogLevel.Trace, message);
		public static LogEntry Debug(string message) => Log(LogLevel.Debug, message);
		public static LogEntry Information(string message) => Log(LogLevel.Information, message);
		public static LogEntry Warning(string message) => Log(LogLevel.Warning, message);
		public static LogEntry Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
		public static LogEntry Critical(string message, Exception? exception = null) => Log(LogLevel.Critical, message, exception);

		// Method to retrieve all logs
		public static IEnumerable<LogEntry> GetAllLogs()
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
