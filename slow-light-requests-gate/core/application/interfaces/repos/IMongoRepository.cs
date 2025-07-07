namespace lazy_light_requests_gate.core.application.interfaces.repos
{
	public interface IMongoRepository<T> : IBaseRepository<T> where T : class
	{
		//IMongoCollection<T> GetCollection();
	}
}
