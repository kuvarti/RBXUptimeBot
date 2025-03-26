using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RBXUptimeBot.Classes;
using RBXUptimeBot.Classes.Services;
using RBXUptimeBot.Models;
using System.Net;
using System.Text.Json.Serialization;

namespace RBXUptimeBot.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class JobController : ControllerBase
	{
		public static JobService _jobS;
		public JobController(ILogger<JobController> logger)
		{
			_jobS = new JobService();
		}

		[HttpPost("start/{text}:{count}")]
		public async Task<ActionResult> Run(long text = 5315046213, int count = 2, [FromQuery] int endTime = 30)
		{
			var exist = AccountManager.ActiveJobs.Find(x => x.JobEntity.PlaceID == text.ToString());

			if (exist != null) {
				exist.AccountCount = count;
				exist.endTime = DateTime.Now.AddMinutes(endTime);
				Logger.Warning($"Job {text} is changed while running.\nNew Account Count: {count}\nNew endTime: {exist.endTime.ToString()}");
				return BadRequest($"Job {text} already exist. changing parameters. Jon now end on {exist.endTime.ToString()}");
			}
			if (AccountManager.AccountsList.Count < 1)
				return BadRequest("There is no account logged in.");
			try
			{
				var msg = await _jobS.JobStarter(text, count, DateTime.Now.AddMinutes(endTime));
				return Ok(msg);
			}
			catch (Exception e)
			{
				return BadRequest(e.Message);
			}
		}

		[HttpPost("finish/{text}")]
		public async Task<ActionResult> close(long text = 5315046213)
		{
			var job = AccountManager.ActiveJobs.Find(x => x.JobEntity.PlaceID == text.ToString());
			if (job == null)
				return BadRequest("Job not found.");
			else
				job.isRunning = false;
			return Ok("Job ended");
		}

		[HttpGet("info")]
		public async Task<ActionResult> info([FromQuery] long text = 0)
		{
			if (text > 0) {
				var job = AccountManager.ActiveJobs.Find(x => x.JobEntity.PlaceID == text.ToString());
				if (job == null)
					return BadRequest("Job not found.");
				return Ok(new {
					DBObject = job.JobEntity,
					AccountCount = $"{job.AccountCount}/{job.ProcessList.Count}",
					Running = job.isRunning,
					StartTime = job.startTime,
					EndTime = job.endTime,
					Processes = job.ProcessList
				});
			}
			List<object> jobs = new List<object>();
			AccountManager.ActiveJobs.ForEach(x => jobs.Add(new
			{
				DBObject = x.JobEntity,
				AccountCount = $"{x.AccountCount}/{x.ProcessList.Count}",
				Running = x.isRunning,
				StartTime = x.startTime,
			}));
			return Ok(jobs);
		}
	}
}
