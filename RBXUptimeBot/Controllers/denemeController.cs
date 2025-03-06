using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PuppeteerSharp;
using RBXUptimeBot.Classes;
using RBXUptimeBot.Models;
using System.Diagnostics;
using System.Net;

namespace RBXUptimeBot.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class denemeController : ControllerBase
	{
		public denemeController(ILogger<denemeController> logger)
		{
		}

		[HttpGet("logs")]
		public async Task<ActionResult> GetLogs()
		{
			//await AccountManager.LogService?.CreateAsync(Logger.Information($"Logs fetched {DateTime.Now.ToString()}."));
			var logs = Logger.GetAllLogs();

			return Ok(logs);
		}

		[HttpPost("makeallnotrunning")]
		public async Task<ActionResult> makeallnotrunning()
		{
			AccountManager.AccountsList.ForEach(account =>
			{
				account.IsActive = 0;
			});
			return Ok();
		}

		[HttpPost("close")]
		public async Task<ActionResult> Close()
		{
			AccountManager.ExitProtocol();
			return Ok();
		}
	}
}
