using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RBXUptimeBot.Classes;
using RBXUptimeBot.Models;
using System.Net;
using System.Text.Json.Serialization;

namespace RBXUptimeBot.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class AccountsController : ControllerBase
	{
		private readonly ILogger<AccountsController> _logger;
		public AccountsController(ILogger<AccountsController> logger)
		{
			_logger = logger;
		}

		[HttpGet("GetAllAccounts")]
		public async Task<ActionResult<List<accountData>>> GetAccounts()
		{
			List<accountData> Accounts = new List<accountData>();

			foreach (var item in AccountManager.AccountsList)
			{
				Accounts.Add(new accountData()
				{
					name = item.Username,
					isRunning = item.IsActive > 10
				});
			}
			return Ok(new { Accounts.Count, Accounts });
		}

		[HttpGet("GetAllRunningAccounts")]
		public async Task<BaseResponseModel> GetAllRunningAccounts()
		{
			List<accountData> data = new List<accountData>();
			foreach (var item in AccountManager.AllRunningAccounts)
			{
				data.Add(new accountData()
				{
					name = item.Account.Username,
					isRunning = true
				});
			}
			return new OkResponseModel(data: new
			{
				TotalRunningAccounts = AccountManager.AllRunningAccounts.Count,
				AllRunningAccounts = data
			});
		}

		[HttpPost("LoginAccounts/{text}")]
		public async Task<ActionResult<string>> LoginAccounts(string text)
		{
			int before = AccountManager.AccountsList.Count;
			//await AccountManager.LoginAccount(text);
			return Ok($"Total logged in account count: '{AccountManager.AccountsList.Count}'. It Was {before} before");
		}

		[HttpPost("LogoutAccounts/{text}")]
		public async Task<ActionResult<string>> LogoutAccounts(string text)
		{
			if (string.IsNullOrEmpty(text))
				return BadRequest("Please provide a username to logout.");
			return Ok(await AccountManager.LogoutAccount(text));
		}
	}
}
