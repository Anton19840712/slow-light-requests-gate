using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using lazy_light_requests_gate.core.application.interfaces.runtime;

namespace lazy_light_requests_gate.presentation.attributes
{
	/// <summary>
	/// Требует, чтобы REST API был включен (RestOnly или Both)
	/// </summary>
	public class RequireRestRuntimeAttribute : ActionFilterAttribute
	{
		public override void OnActionExecuting(ActionExecutingContext context)
		{
			var appTypeService = context.HttpContext.RequestServices
				.GetRequiredService<IApplicationTypeService>();

			if (!appTypeService.IsRestEnabled())
			{
				var currentType = appTypeService.GetApplicationType();
				context.Result = new ObjectResult(new
				{
					message = "This endpoint requires REST API to be enabled",
					currentType = currentType.ToString(),
					requiredMode = "REST API enabled",
					availableModes = new[] { "RestOnly", "Both" },
					canEnableBy = new[]
					{
						"POST /api/ApplicationType/switch with type='RestOnly'",
						"POST /api/ApplicationType/switch with type='Both'"
					},
					timestamp = DateTime.UtcNow
				})
				{
					StatusCode = 503 // Service Unavailable
				};
			}
		}
	}
}
