namespace lazy_light_requests_gate.repositories
{
	public abstract class BaseRepository<T> : IBaseRepository<T> where T : class
	{
		public abstract Task<IEnumerable<T>> GetAllAsync();
		public abstract Task InsertAsync(T entity);

		public virtual async Task<List<T>> GetUnprocessedMessagesAsync()
		{
			var messages = await GetUnprocessedMessagesInternalAsync();
			return messages.ToList();
		}

		public virtual async Task MarkMessageAsProcessedAsync(Guid messageId)
		{
			await MarkMessageAsProcessedInternalAsync(messageId);
		}

		public virtual async Task<int> DeleteOldMessagesAsync(TimeSpan olderThan)
		{
			return await DeleteOldMessagesInternalAsync(olderThan);
		}

		public virtual async Task SaveMessageAsync(T message)
		{
			await InsertAsync(message);
		}

		// Абстрактные методы для специфичной реализации каждого провайдера
		protected abstract Task<IEnumerable<T>> GetUnprocessedMessagesInternalAsync();
		protected abstract Task MarkMessageAsProcessedInternalAsync(Guid messageId);
		protected abstract Task<int> DeleteOldMessagesInternalAsync(TimeSpan olderThan);

		public abstract Task UpdateMessageAsync(T message);
	}
}
