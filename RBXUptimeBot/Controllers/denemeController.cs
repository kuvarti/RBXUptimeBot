using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PuppeteerSharp;
using RBXUptimeBot.Classes;
using RBXUptimeBot.Models;
using System.Diagnostics;
using System.Net;
using WebSocketSharp;
using Logger = RBXUptimeBot.Classes.Logger;

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

		[HttpPost("ChangeSettingParameter/{Section}")]
		public async Task<ActionResult> ChangeSettingParameter(string Section, [FromQuery] string parameter, [FromQuery] string value)
		{
			if (Section.IsNullOrEmpty()) return BadRequest("Section is null or empty.");
			if (parameter.IsNullOrEmpty()) return BadRequest("Parameter is null or empty.");

			var _sec = AccountManager.IniList[Section];
			if (_sec == null) return BadRequest("Section not found.");
			if (_sec.Exists(parameter))
			{
				if (value.IsNullOrEmpty()) _sec.RemoveProperty(parameter);
				else _sec.Set(parameter, value);
			}
			else return BadRequest("Parameter not found.");
			AccountManager.IniSettings.Save("RAMSettings.ini");
			return Ok($"[{Section}] {parameter}: {_sec.Get(parameter)}");
		}

		[HttpGet("GetAllCommands")]
		public async Task<ActionResult> GetAllCommands()
		{
			var commands = new List<string>();
			var a = Process.GetProcessesByName("RobloxPlayerBeta");
			foreach (var item in a)
			{
				try
				{
					commands.Add(item.GetCommandLine());
				}
				catch (Exception ex)
				{
					commands.Add(ex.Message);
				}
			}
			return Ok(commands);
		}

		[HttpPost("close")]
		public async Task<ActionResult> Close()
		{
			AccountManager.ExitProtocol();
			return Ok();
		}
	}
}
