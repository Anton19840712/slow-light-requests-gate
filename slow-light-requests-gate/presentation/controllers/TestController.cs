using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.сontrollers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
	private readonly ILogger<TestController> _logger;
	private readonly IConfiguration _configuration;
	private readonly IWebHostEnvironment _env;
	private readonly string _instanceId;
	public TestController(
		ILogger<TestController> logger,
		IConfiguration configuration,
		IWebHostEnvironment env,
		string instanceId)
	{
		_instanceId = instanceId;
		_logger = logger;
		_configuration = configuration;
		_env = env;
		_instanceId = instanceId;
	}

	// Простой GET
	[HttpGet("ping")]
	public IActionResult Ping()
	{
		return Ok("pong");
	}

	// Простой POST
	[HttpPost("echo")]
	public IActionResult Echo([FromBody] object data)
	{
		return Ok(new { received = data });
	}

	// GET, который показывает адрес и среду окружения
	[HttpGet("info")]
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
}
