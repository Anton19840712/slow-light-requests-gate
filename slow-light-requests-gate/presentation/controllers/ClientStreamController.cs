using infrastructure.networking;
using lazy_light_requests_gate.temp.apptypeswitcher;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class ClientStreamController : ControllerBase
	{
		private readonly NetworkClientManager _manager;

		public ClientStreamController(NetworkClientManager manager)
		{
			_manager = manager;
		}

		[HttpPost("start/{protocol}")]
		[RequireStreamRuntime]
		public async Task<IActionResult> Start(string protocol)
		{
			try
			{
				await _manager.StartClientAsync(protocol, HttpContext.RequestAborted);
				return Ok($"Клиент {protocol} запущен.");
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
				await _manager.StopClientAsync(protocol, HttpContext.RequestAborted);
				return Ok($"Клиент {protocol} остановлен.");
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
			var running = _manager.GetRunningClients();
			return Ok(running);
		}
	}
}
