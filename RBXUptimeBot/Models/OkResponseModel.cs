using System.Net;

namespace RBXUptimeBot.Models
{
	public class OkResponseModel : BaseResponseModel
	{
		public OkResponseModel(string message= "Success", object data=null)
		{
			_statusCode = HttpStatusCode.OK;
			_message = message;
			_data = data;
		}
	}
}
