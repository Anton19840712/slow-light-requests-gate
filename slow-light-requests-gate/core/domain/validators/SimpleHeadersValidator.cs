using lazy_light_requests_gate.core.application.interfaces.headers;
using lazy_light_requests_gate.temp.models;

namespace lazy_light_requests_gate.core.application.services.validators
{
	public class SimpleHeadersValidator : IHeadersValidator
	{
		public Task<ResponseIntegration> ValidateHeadersAsync(IHeaderDictionary headers)
		{
			// Минимальная проверка: просто наличие или отсутствие X-Custom-Header
			if (!headers.ContainsKey("X-Custom-Header"))
			{
				return Task.FromResult(new ResponseIntegration
				{
					Message = "Missing required header: X-Custom-Header",
					Result = false
				});
			}

			return Task.FromResult(new ResponseIntegration
			{
				Message = "Headers are valid.",
				Result = true
			});
		}
	}
}
