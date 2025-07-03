using lazy_light_requests_gate.presentation.models.response;

namespace lazy_light_requests_gate.core.application.interfaces.headers
{
	public interface IHeadersValidator
	{
		Task<ResponseIntegration> ValidateHeadersAsync(IHeaderDictionary headers);
	}
}
