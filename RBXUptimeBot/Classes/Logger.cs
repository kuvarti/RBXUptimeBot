using log4net;
using log4net.Appender;
using log4net.Core;
using System.Collections.Concurrent;
using System.Text.Json;

namespace RBXUptimeBot.Classes
{
	public static class Logger
	{
		// Define a thread-safe collection to store log entries
		private static readonly ConcurrentQueue<LogEntry> _logEntries = new ConcurrentQueue<LogEntry>();

		// Enum for log levels
		public enum LogLevel
		{
			Trace,
			Debug,
			Information,
			Warning,
			Error,
			Critical
		}

		public class SerializableException
		{
			public string Message { get; set; }
			public string StackTrace { get; set; }
			public string Source { get; set; }
			public string TargetSite { get; set; }
			public string ExceptionType { get; set; }
			public SerializableException InnerException { get; set; }
			public Dictionary<string, object> Data { get; set; }
		}

		// Log Entry Model
		public class LogEntry
		{
			public DateTime Timestamp { get; set; }
			public LogLevel Level { get; set; }
			public string Message { get; set; }
			public SerializableException? Exception { get; set; }
		}

		// Method to log a message with a specific level
		public static void Log(LogLevel level, string message, Exception? exception = null)
		{
			var logEntry = new LogEntry
			{
				Timestamp = DateTime.UtcNow,
				Level = level,
				Message = message,
				Exception = exception?.ToSerializable()
			};
			_logEntries.Enqueue(logEntry);
		}

		// Convenience methods for different log levels
		public static void Trace(string message) => Log(LogLevel.Trace, message);
		public static void Debug(string message) => Log(LogLevel.Debug, message);
		public static void Information(string message) => Log(LogLevel.Information, message);
		public static void Warning(string message) => Log(LogLevel.Warning, message);
		public static void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
		public static void Critical(string message, Exception? exception = null) => Log(LogLevel.Critical, message, exception);

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
		public static Logger.SerializableException ToSerializable(this Exception ex)
		{
			if (ex == null) return null;

			return new Logger.SerializableException
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
