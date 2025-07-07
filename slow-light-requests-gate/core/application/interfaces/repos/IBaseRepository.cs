namespace lazy_light_requests_gate.core.application.interfaces.repos
{
	public interface IBaseRepository<T> where T : class
	{
		Task InsertAsync(T entity);
		Task<List<T>> GetUnprocessedMessagesAsync();
		Task<int> DeleteOldRecordsAsync(DateTime cutoffDate, bool requireProcessed = false);
		Task SaveMessageAsync(T message);
		Task UpdateMessagesAsync(IEnumerable<T> messages);
	}
}
