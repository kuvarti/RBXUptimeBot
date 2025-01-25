using Newtonsoft.Json;
using System.Net;
using System.Text.Json.Serialization;

namespace RBXUptimeBot.Models
{
	public class BaseResponseModel
	{
		[JsonPropertyName("statusCode")]
		public HttpStatusCode _statusCode { get; set; }
		[JsonPropertyName("message")]
		public string _message { get; set; }
		[JsonPropertyName("data")]
		public object _data { get; set; }
	}
}
