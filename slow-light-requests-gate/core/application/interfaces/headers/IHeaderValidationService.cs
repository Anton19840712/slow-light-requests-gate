namespace lazy_light_requests_gate.core.application.interfaces.headers
{
	// Интерфейс для сервиса валидации заголовков:
	public interface IHeaderValidationService
	{
		Task<bool> ValidateHeadersAsync(IHeaderDictionary headers);
	}
}
