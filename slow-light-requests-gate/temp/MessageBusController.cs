using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace lazy_light_requests_gate.temp
{
	[ApiController]
	[Route("api/messagebus")]
	public class MessageBusController : ControllerBase
	{
		private readonly IUnifiedMessageBusManager _busManager;

		public MessageBusController(IUnifiedMessageBusManager busManager)
		{
			_busManager = busManager;
		}

		[HttpPost("start")]
		public async Task<IActionResult> Start([FromBody] JsonDocument configJson)
		{
			try
			{
				await _busManager.StartViaRestRequetAsync(configJson, HttpContext.RequestAborted);
				return Ok("Шина запущена");
			}
			catch (Exception ex)
			{
				return BadRequest(ex.Message);
			}
		}

		[HttpPost("stop/{id}")]
		public async Task<IActionResult> Stop(string id)
		{
			try
			{
				await _busManager.StopBusAsync(id, HttpContext.RequestAborted);
				return Ok($"Шина с id {id} остановлена.");
			}
			catch (Exception ex)
			{
				return BadRequest(ex.Message);
			}
		}

		[HttpGet("status")]
		public IActionResult GetStatus()
		{
			var runningBuses = _busManager.GetRunningBusIds();
			return Ok(new { RunningBuses = runningBuses, Count = runningBuses.Count() });
		}

		[HttpGet("allinfo")]
		public IActionResult GetInstancesInfo()
		{
			var runningBuses = _busManager.GetRunningBusInfo().ToList();
			return Ok(new { RunningBuses = runningBuses, Count = runningBuses.Count() });
		}
	}
}
