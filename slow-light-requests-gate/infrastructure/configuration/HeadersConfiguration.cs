using lazy_light_requests_gate.core.application.interfaces.headers;
using lazy_light_requests_gate.core.application.services.validators;

namespace lazy_light_requests_gate.infrastructure.configuration
{
	static class HeadersConfiguration
	{
		/// <summary>
		/// Регистрация сервисов заголовков.
		/// </summary>
		public static IServiceCollection AddHeadersServices(this IServiceCollection services)
		{
			services.AddTransient<SimpleHeadersValidator>();
			services.AddTransient<DetailedHeadersValidator>();
			services.AddTransient<IHeaderValidationService, HeaderValidationService>();

			return services;
		}
	}
}
