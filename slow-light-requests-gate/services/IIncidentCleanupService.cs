namespace lazy_light_requests_gate.services
{
	namespace lazy_light_requests_gate.services
	{
		public interface IIncidentCleanupService
		{
			Task StartIncidentCleanupAsync(IServiceProvider provider, CancellationToken cancellationToken);
		}
	}
}
