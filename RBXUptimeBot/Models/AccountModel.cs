using System.Text.Json.Serialization;

namespace RBXUptimeBot.Models
{
	public struct accountData {
		public bool isRunning { get; set; }
		public string name { get; set; }
	}
	public class AccountModel
	{
		public BaseResponseModel _response { get; set; }

		public AccountModel(BaseResponseModel response, accountData data)
		{
			this._response = response;
			this._response._data = data;
		}
	}
}
