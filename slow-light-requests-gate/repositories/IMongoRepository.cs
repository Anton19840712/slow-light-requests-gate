using System.Linq.Expressions;

namespace lazy_light_requests_gate.repositories
{
	public interface IMongoRepository<T> where T : class
	{
		Task<T> GetByIdAsync(string id);
		Task<IEnumerable<T>> GetAllAsync();
		Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> filter);
		Task InsertAsync(T entity);
		Task UpdateAsync(string id, T entity);
		Task DeleteByIdAsync(string id);
		Task<int> DeleteByTtlAsync(TimeSpan olderThan);

		Task SaveMessageAsync(T message);
		Task<List<T>> GetUnprocessedMessagesAsync();
		Task MarkMessageAsProcessedAsync(string messageId);
		Task<int> DeleteOldMessagesAsync(TimeSpan olderThan);
	}
}
