namespace lazy_light_requests_gate.repositories
{
	public interface IPostgresRepository<T> where T : class
	{
		Task<T> GetByIdAsync(Guid id);
		Task<IEnumerable<T>> GetAllAsync();
		Task<IEnumerable<T>> FindAsync(string whereClause, object parameters = null);
		Task InsertAsync(T entity);
		Task UpdateAsync(Guid id, T entity);
		Task DeleteByIdAsync(Guid id);
		Task<int> DeleteByTtlAsync(TimeSpan olderThan);

		Task SaveMessageAsync(T message);
		Task<List<T>> GetUnprocessedMessagesAsync();
		Task MarkMessageAsProcessedAsync(Guid messageId);
		Task<int> DeleteOldMessagesAsync(TimeSpan olderThan);
	}
}
