using lazy_light_requests_gate.configurationsettings;
using lazy_light_requests_gate.entities;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;
using System.Linq.Expressions;

namespace lazy_light_requests_gate.repositories
{
	public class MongoRepository<T> : IMongoRepository<T> where T : class
	{
		private readonly IMongoCollection<T> _collection;

		public MongoRepository(
			IMongoClient mongoClient,
			IOptions<MongoDbSettings> settings)
		{
			var database = mongoClient.GetDatabase(settings.Value.DatabaseName);

			string collectionName = GetCollectionName(typeof(T), settings.Value);
			_collection = database.GetCollection<T>(collectionName);

			try
			{
				database.ListCollectionNames().ToList(); // Простой запрос для проверки подключения
				Log.Information($"Успешное подключение к MongoDB в базе: {settings.Value.DatabaseName}, коллекция: {collectionName}");
			}
			catch (Exception ex)
			{
				// Логируем ошибку, если подключение не удалось
				Log.Error($"Ошибка при подключении к MongoDB в базе: {settings.Value.DatabaseName}, коллекция: {collectionName}. Ошибка: {ex.Message}");
				throw new InvalidOperationException("Не удалось подключиться к MongoDB", ex);
			}
		}

		private string GetCollectionName(Type entityType, MongoDbSettings settings)
		{
			return entityType.Name switch
			{
				nameof(OutboxMessage) => settings.Collections.OutboxCollection,
				nameof(QueuesEntity) => settings.Collections.QueueCollection,
				nameof(IncidentEntity) => settings.Collections.IncidentCollection,
				_ => throw new ArgumentException($"Неизвестный тип {entityType.Name}")
			};
		}

		public async Task<T> GetByIdAsync(string id) =>
			await _collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync();

		public async Task<IEnumerable<T>> GetAllAsync() =>
			await _collection.Find(_ => true).ToListAsync();

		public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> filter) =>
			await _collection.Find(filter).ToListAsync();

		public async Task InsertAsync(T entity) =>
			await _collection.InsertOneAsync(entity);

		public async Task UpdateAsync(string id, T updatedEntity)
		{
			var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
			var existingEntity = await _collection.Find(filter).FirstOrDefaultAsync();

			if (existingEntity == null)
			{
				throw new InvalidOperationException($"Документ с ID {id} не найден");
			}

			var updateDefinitionBuilder = Builders<T>.Update;
			var updates = new List<UpdateDefinition<T>>();

			// Перебираем все свойства модели
			foreach (var property in typeof(T).GetProperties())
			{
				if (property.Name == "Version") continue; // Пропускаем Version, т.к. он изменяется отдельно

				var oldValue = property.GetValue(existingEntity);
				var newValue = property.GetValue(updatedEntity);

				if (newValue != null && !newValue.Equals(oldValue))
				{
					updates.Add(updateDefinitionBuilder.Set(property.Name, newValue));
				}
			}

			// Добавляем обновление времени
			updates.Add(updateDefinitionBuilder.Set("UpdatedAtUtc", DateTime.UtcNow));
			updates.Add(updateDefinitionBuilder.Inc("Version", 1));

			if (updates.Count > 0)
			{
				var updateDefinition = updateDefinitionBuilder.Combine(updates);
				await _collection.UpdateOneAsync(filter, updateDefinition);
			}
		}

		public async Task DeleteByIdAsync(string id) =>
			await _collection.DeleteOneAsync(Builders<T>.Filter.Eq("_id", id));

		public async Task<int> DeleteByTtlAsync(TimeSpan olderThan)
		{
			if (typeof(T) != typeof(OutboxMessage))
			{
				throw new InvalidOperationException("Метод поддерживает только OutboxMessage.");
			}

			var filter = Builders<OutboxMessage>.Filter.And(
				Builders<OutboxMessage>.Filter.Lt(m => m.CreatedAt, DateTime.UtcNow - olderThan),
				Builders<OutboxMessage>.Filter.Eq(m => m.IsProcessed, true)
			);

			var collection = _collection as IMongoCollection<OutboxMessage>;
			var result = await collection.DeleteManyAsync(filter);

			return (int)result.DeletedCount;
		}
		public async Task<List<T>> GetUnprocessedMessagesAsync()
		{
			var filter = Builders<T>.Filter.Eq("IsProcessed", false);
			return await _collection.Find(filter).ToListAsync();
		}

		public async Task MarkMessageAsProcessedAsync(string messageId)
		{
			var update = Builders<T>.Update
				.Set("IsProcessed", true)
				.Set("ProcessedAt", DateTime.UtcNow);

			await _collection.UpdateOneAsync(Builders<T>.Filter.Eq("Id", messageId), update);
		}

		public async Task<int> DeleteOldMessagesAsync(TimeSpan olderThan)
		{
			var filter = Builders<T>.Filter.And(
				Builders<T>.Filter.Lt("CreatedAt", DateTime.UtcNow - olderThan),
				Builders<T>.Filter.Eq("IsProcessed", true)
			);

			var result = await _collection.DeleteManyAsync(filter);
			return (int)result.DeletedCount;
		}

		public async Task SaveMessageAsync(T message)
		{
			await _collection.InsertOneAsync(message);
		}
	}
}
