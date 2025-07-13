using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using lazy_light_requests_gate.presentation.enums;

namespace lazy_light_requests_gate.temp.apptypeswitcher
{
	/// <summary>
	/// Требует, чтобы хотя бы один API был включен (любой режим кроме отключенного)
	/// </summary>
	public class RequireEitherAPIRuntimeAttribute : ActionFilterAttribute
	{
		public override void OnActionExecuting(ActionExecutingContext context)
		{
			var appTypeService = context.HttpContext.RequestServices
				.GetRequiredService<IApplicationTypeService>();

			// Проверяем, что включен ХОТЯ БЫ ОДИН из API (REST или Stream)
			if (!appTypeService.IsRestEnabled() && !appTypeService.IsStreamEnabled())
			{
				var currentType = appTypeService.GetApplicationType();
				context.Result = new ObjectResult(new
				{
					message = "This endpoint requires at least one API to be enabled (REST or Stream)",
					currentType = currentType.ToString(),
					enabledAPIs = GetEnabledAPIs(appTypeService),
					availableModes = new[] { "RestOnly", "StreamOnly", "Both" },
					canEnableBy = new[]
					{
						"POST /api/ApplicationType/switch with type='RestOnly'",
						"POST /api/ApplicationType/switch with type='StreamOnly'",
						"POST /api/ApplicationType/switch with type='Both'"
					},
					timestamp = DateTime.UtcNow
				})
				{
					StatusCode = 503 // Service Unavailable
				};
			}
		}

		private static string[] GetEnabledAPIs(IApplicationTypeService service)
		{
			var enabled = new List<string>();
			if (service.IsRestEnabled()) enabled.Add("REST");
			if (service.IsStreamEnabled()) enabled.Add("Stream");
			return enabled.ToArray();
		}
	}
}