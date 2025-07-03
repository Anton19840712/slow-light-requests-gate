namespace lazy_light_requests_gate.core.application.interfaces.repos
{
	public interface IBaseRepository<T> where T : class
	{
		Task<IEnumerable<T>> GetAllAsync();
		Task InsertAsync(T entity);
		Task<List<T>> GetUnprocessedMessagesAsync();
		Task MarkMessageAsProcessedAsync(Guid messageId);
		Task<int> DeleteOldRecordsAsync(DateTime cutoffDate, bool requireProcessed = false);
		Task SaveMessageAsync(T message);
		Task UpdateMessageAsync(T message);
		Task UpdateMessagesAsync(IEnumerable<T> messages);
		Task InsertMessagesAsync(IEnumerable<T> messages);
		Task DeleteMessagesAsync(IEnumerable<Guid> messageIds);
	}
}
