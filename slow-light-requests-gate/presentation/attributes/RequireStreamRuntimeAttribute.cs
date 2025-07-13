using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using lazy_light_requests_gate.core.application.interfaces.runtime;

namespace lazy_light_requests_gate.presentation.attributes
{
	/// <summary>
	/// Требует, чтобы Stream API был включен (StreamOnly или Both)
	/// </summary>
	public class RequireStreamRuntimeAttribute : ActionFilterAttribute
	{
		public override void OnActionExecuting(ActionExecutingContext context)
		{
			var appTypeService = context.HttpContext.RequestServices
				.GetRequiredService<IApplicationTypeService>();

			// ✅ ИСПРАВЛЕНО: Проверяем, что Stream включен (StreamOnly или Both)
			if (!appTypeService.IsStreamEnabled())
			{
				var currentType = appTypeService.GetApplicationType();
				context.Result = new ObjectResult(new
				{
					message = "This endpoint requires Stream API to be enabled",
					currentType = currentType.ToString(),
					requiredMode = "Stream API enabled",
					availableModes = new[] { "StreamOnly", "Both" },
					canEnableBy = new[]
					{
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
	}
}
