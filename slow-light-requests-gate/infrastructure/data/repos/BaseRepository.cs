using lazy_light_requests_gate.core.application.interfaces.repos;

namespace lazy_light_requests_gate.infrastructure.data.repos
{
	public abstract class BaseRepository<T> : IBaseRepository<T> where T : class
	{
		public abstract Task<IEnumerable<T>> GetAllAsync();
		public abstract Task InsertAsync(T entity);
		public abstract Task<List<T>> GetUnprocessedMessagesAsync();
		public abstract Task MarkMessageAsProcessedAsync(Guid messageId);
		public abstract Task UpdateMessageAsync(T message);
		public abstract Task UpdateMessagesAsync(IEnumerable<T> messages);
		public abstract Task InsertMessagesAsync(IEnumerable<T> messages);
		public abstract Task<int> DeleteOldRecordsAsync(DateTime cutoffDate, bool requireProcessed = false);
		public abstract Task DeleteMessagesAsync(IEnumerable<Guid> messageIds);
		public abstract Task SaveMessageAsync(T message);
	}
}
