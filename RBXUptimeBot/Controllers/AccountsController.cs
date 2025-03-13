using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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
		private bool _LoginProcess;
		private readonly ILogger<AccountsController> _logger;
		public AccountsController(ILogger<AccountsController> logger)
		{
			_LoginProcess = false;
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
		public async Task<ActionResult> GetAllRunningAccounts()
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
			return Ok(new
			{
				TotalRunningAccounts = AccountManager.AllRunningAccounts.Count,
				AllRunningAccounts = data
			});
		}

		[HttpPost("StartAccountsLogin")]
		public async Task<ActionResult> StartAccountsLogin()
		{
			if (_LoginProcess) return Ok("Login Process already begin.");
			var res = AccountManager.InitAccounts();
			if (res.Item1) return Ok(res.Item2);
			else return BadRequest(res.Item2);
		}

		[HttpGet("GetAccountLoginLogs")]
		public async Task<ActionResult> GetAccountLoginLogs()
		{
			string res = $"There is {AccountManager.maxAcc} account fetched from sheet\n";
			res += $"{AccountManager.AccountsList.Count} of them logged in.\n";
			res += $"{AccountManager.Machine.Get<int>("MaxAccountLoggedIn")} account will be logged in end of the progress if it possible.\n";
			return Ok(res);
		}
	}
}
