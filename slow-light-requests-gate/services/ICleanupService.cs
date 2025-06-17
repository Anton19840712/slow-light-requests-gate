using lazy_light_requests_gate.repositories;

namespace lazy_light_requests_gate.services
{
	public interface ICleanupService<TRepository> where TRepository : IBaseRepository<OutboxMessage>
	{
		Task StartCleanupAsync(TRepository repository, CancellationToken cancellationToken);
	}
}
