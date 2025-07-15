using lazy_light_requests_gate.core.application.interfaces.runtime;
using lazy_light_requests_gate.presentation.attributes;
using lazy_light_requests_gate.presentation.enums;
using Microsoft.AspNetCore.Mvc;

namespace lazy_light_requests_gate.presentation.controllers
{
	// Контроллер для управления типом приложения
	[ApiController]
	[Route("api/[controller]")]
	public class ApplicationTypeController : BaseGatewayController
	{
		private readonly IApplicationTypeService _applicationTypeService;

		public ApplicationTypeController(
			IApplicationTypeService applicationTypeService,
			ILogger<ApplicationTypeController> logger) : base(logger)
		{
			_applicationTypeService = applicationTypeService;
		}

		/// <summary>
		/// Получить текущий тип приложения
		/// </summary>		
		[HttpGet("current")]
		[RequireEitherAPIRuntime]
		public IActionResult GetCurrentApplicationType()
		{
			try
			{
				var applicationType = _applicationTypeService.GetApplicationType();

				return Ok(new
				{
					type = applicationType.ToString(),
					description = _applicationTypeService.GetDescription(),
					isRestEnabled = _applicationTypeService.IsRestEnabled(),
					isStreamEnabled = _applicationTypeService.IsStreamEnabled(),
					isBothEnabled = _applicationTypeService.IsBothEnabled(),
					timestamp = DateTime.UtcNow
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Ошибка получения типа приложения");
				return StatusCode(500, new { message = "Ошибка получения типа приложения", error = ex.Message });
			}
		}

		/// <summary>
		/// Получить поддерживаемые типы приложения
		/// </summary>
		[HttpGet("supported")]
		[RequireEitherAPIRuntime]
		public IActionResult GetSupportedApplicationTypes()
		{
			return Ok(new
			{
				supportedTypes = new[] { "restonly", "streamonly", "both" },
				descriptions = new Dictionary<string, string>
			{
				{ "restonly", "Только REST API" },
				{ "streamonly", "Только Stream протоколы" },
				{ "both", "REST API и Stream протоколы" }
			},
				currentType = _applicationTypeService.GetApplicationType().ToString().ToLowerInvariant(),
				timestamp = DateTime.UtcNow
			});
		}

		/// <summary>
		/// Получить статус компонентов
		/// </summary>
		[HttpGet("status")]
		[RequireEitherAPIRuntime]
		public IActionResult GetComponentsStatus()
		{
			return Ok(new
			{
				currentType = _applicationTypeService.GetApplicationType().ToString(),
				components = new
				{
					restApi = new
					{
						enabled = _applicationTypeService.IsRestEnabled(),
						status = _applicationTypeService.IsRestEnabled() ? "Active" : "Disabled"
					},
					streamProtocols = new
					{
						enabled = _applicationTypeService.IsStreamEnabled(),
						status = _applicationTypeService.IsStreamEnabled() ? "Active" : "Disabled"
					}
				},
				timestamp = DateTime.UtcNow
			});
		}

		[HttpPost("switch")]
		[RequireEitherAPIRuntime]
		public async Task<IActionResult> SwitchApplicationType([FromBody] SwitchApplicationTypeRequest request)
		{
			return await SafeExecuteAsync(async () =>
			{
				if (!Enum.TryParse<ApplicationType>(request.Type, true, out var newType))
				{
					return ErrorResponse("Invalid application type", 400, new
					{
						validTypes = Enum.GetNames(typeof(ApplicationType)),
						provided = request.Type
					});
				}

				var oldType = _applicationTypeService.GetApplicationType();
				await _applicationTypeService.SetApplicationTypeAsync(newType);

				return SuccessResponse(new
				{
					message = "Application type switched successfully",
					oldType = oldType.ToString(),
					newType = newType.ToString(),
					description = _applicationTypeService.GetDescription(),
					isRestEnabled = _applicationTypeService.IsRestEnabled(),
					isStreamEnabled = _applicationTypeService.IsStreamEnabled(),
					isBothEnabled = _applicationTypeService.IsBothEnabled(),
					timestamp = DateTime.UtcNow
				});
			}, "SwitchApplicationType", "Application type switched successfully", request);
		}

		[HttpGet("switch-history")]
		[RequireEitherAPIRuntime]
		public IActionResult GetSwitchHistory()
		{
			// Здесь можно вернуть историю переключений из базы данных
			return Ok(new
			{
				message = "Switch history would be implemented here",
				currentType = _applicationTypeService.GetApplicationType().ToString(),
				timestamp = DateTime.UtcNow
			});
		}

		public class SwitchApplicationTypeRequest
		{
			public string Type { get; set; }
		}
	}
}
