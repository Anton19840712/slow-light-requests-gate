namespace lazy_light_requests_gate.core.application.interfaces.repos
{
	public interface IPostgresRepository<T> : IBaseRepository<T> where T : class
	{
	}
}
