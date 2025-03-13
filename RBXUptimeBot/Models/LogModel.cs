namespace RBXUptimeBot.Models
{
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
}
