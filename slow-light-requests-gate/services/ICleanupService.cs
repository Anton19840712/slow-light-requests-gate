using lazy_light_requests_gate.repositories;

namespace lazy_light_requests_gate.services
{
	public interface ICleanupService
	{
		Task StartCleanupAsync(IServiceProvider provider, CancellationToken cancellationToken);
	}
}
