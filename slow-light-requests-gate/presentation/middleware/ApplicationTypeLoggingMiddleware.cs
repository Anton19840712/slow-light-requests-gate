using lazy_light_requests_gate.core.application.interfaces.runtime;
using lazy_light_requests_gate.core.domain.events;

namespace lazy_light_requests_gate.presentation.middleware
{
	public class ApplicationTypeLoggingMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<ApplicationTypeLoggingMiddleware> _logger;

		public ApplicationTypeLoggingMiddleware(RequestDelegate next, ILogger<ApplicationTypeLoggingMiddleware> logger)
		{
			_next = next;
			_logger = logger;
		}

		public async Task InvokeAsync(HttpContext context, IApplicationTypeService appTypeService)
		{
			// Подписываемся на события изменения типа приложения
			appTypeService.ApplicationTypeChanged += OnApplicationTypeChanged;

			try
			{
				await _next(context);
			}
			finally
			{
				// Отписываемся от событий
				appTypeService.ApplicationTypeChanged -= OnApplicationTypeChanged;
			}
		}

		private void OnApplicationTypeChanged(object sender, ApplicationTypeChangedEventArgs e)
		{
			_logger.LogInformation("Application type changed: {OldType} -> {NewType} at {Timestamp}",
				e.OldType, e.NewType, e.Timestamp);
		}
	}
}
