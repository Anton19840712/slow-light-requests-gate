using lazy_light_requests_gate.temp.models;

namespace lazy_light_requests_gate.core.application.interfaces.headers
{
	public interface IHeadersValidator
	{
		Task<ResponseIntegration> ValidateHeadersAsync(IHeaderDictionary headers);
	}
}
