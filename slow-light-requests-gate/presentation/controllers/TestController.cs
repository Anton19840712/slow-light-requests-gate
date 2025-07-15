using lazy_light_requests_gate.presentation.attributes;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : BaseGatewayController
{
	private readonly IWebHostEnvironment _env;
	private readonly string _instanceId;

	public TestController(
		ILogger<TestController> logger,
		IWebHostEnvironment env,
		string instanceId) : base(logger)
	{
		_instanceId = instanceId;
		_env = env;
	}

	// EITHER api:
	[HttpGet("ping")]
	[RequireEitherAPIRuntime]
	public IActionResult Ping()
	{
		return Ok("pong");
	}

	[HttpPost("echo")]
	[RequireEitherAPIRuntime]
	public IActionResult Echo([FromBody] object data)
	{
		return Ok(new { received = data });
	}

	[HttpGet("info")]
	[RequireEitherAPIRuntime]
	public IActionResult GetAppInfo()
	{
		var scheme = Request.Scheme;
		var host = Request.Host;
		var envName = _env.EnvironmentName;

		return Ok(new
		{
			Environment = envName,
			Url = $"{scheme}://{host}/api/test"
		});
	}

	[HttpGet("instance")]
	[RequireEitherAPIRuntime]
	public IActionResult GetInstanceInfo()
	{
		var address = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
		return Ok(new
		{
			InstanceId = _instanceId,
			Host = Environment.MachineName,
			Endpoint = address
		});
	}


	// REST api:
	[HttpGet("data")]
	[RequireRestRuntime]
	public IActionResult GetRestData() => Ok(new { type = "REST", data = "REST only data", timestamp = DateTime.UtcNow });

	[HttpPost("process")]
	[RequireRestRuntime]
	public IActionResult ProcessRestData([FromBody] object data) => Ok(new { type = "REST", processed = data, timestamp = DateTime.UtcNow });

	[HttpGet("status")]
	[RequireRestRuntime]
	public IActionResult GetRestStatus() => Ok(new { type = "REST", status = "active", mode = "RestOnly", timestamp = DateTime.UtcNow });

	// STREAM api:
	[HttpGet("stream")]
	[RequireStreamRuntime]
	public IActionResult GetStreamData() => Ok(new { type = "STREAM", data = "Stream only data", timestamp = DateTime.UtcNow });

	[HttpPost("notify")]
	[RequireStreamRuntime]
	public IActionResult SendNotification([FromBody] object notification) => Ok(new { type = "STREAM", sent = notification, timestamp = DateTime.UtcNow });

	[HttpGet("live")]
	[RequireStreamRuntime]
	public IActionResult GetLiveData() => Ok(new { type = "STREAM", live = true, mode = "StreamOnly", timestamp = DateTime.UtcNow });

}
