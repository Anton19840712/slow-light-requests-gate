using infrastructure.networking;
using lazy_light_requests_gate.temp.apptypeswitcher;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class ServerStreamController : ControllerBase
	{
		private readonly NetworkServerManager _manager;

		public ServerStreamController(NetworkServerManager manager)
		{
			_manager = manager;
		}

		[HttpPost("start/{protocol}")]
		[RequireStreamRuntime]
		public async Task<IActionResult> Start(string protocol)
		{
			try
			{
				await _manager.StartServerAsync(protocol, HttpContext.RequestAborted);
				return Ok($"Сервер {protocol} запущен.");
			}
			catch (Exception ex)
			{
				return BadRequest(ex.Message);
			}
		}

		[HttpPost("stop/{protocol}")]
		[RequireStreamRuntime]
		public async Task<IActionResult> Stop(string protocol)
		{
			try
			{
				await _manager.StopServerAsync(protocol, HttpContext.RequestAborted);
				return Ok($"Сервер {protocol} остановлен.");
			}
			catch (Exception ex)
			{
				return BadRequest(ex.Message);
			}
		}

		[HttpGet("status")]
		[RequireStreamRuntime]
		public IActionResult Status()
		{
			var running = _manager.GetRunningServers();
			return Ok(running);
		}
	}
}
