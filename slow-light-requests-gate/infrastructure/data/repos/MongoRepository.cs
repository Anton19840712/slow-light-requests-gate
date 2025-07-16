using lazy_light_requests_gate.core.application.interfaces.databases;
using lazy_light_requests_gate.core.application.interfaces.repos;
using lazy_light_requests_gate.core.domain.entities;
using lazy_light_requests_gate.core.domain.settings.databases;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;
using System.Linq.Expressions;

namespace lazy_light_requests_gate.infrastructure.data.repos
{
	public class MongoRepository<T> : IMongoRepository<T> where T : class
	{
		private readonly IDynamicMongoClient _dynamicClient;
		private readonly IOptions<MongoDbSettings> _settings;
		private readonly IDynamicDatabaseManager _databaseManager;
		private readonly Guid _instanceId = Guid.NewGuid();

		public MongoRepository(
			IDynamicMongoClient dynamicClient,
			IOptions<MongoDbSettings> settings,
			IDynamicDatabaseManager databaseManager)
		{
			_dynamicClient = dynamicClient;
			_settings = settings;
			_databaseManager = databaseManager;

			// Log.Information($"MongoRepository<{typeof(T).Name}> создан с ID {_instanceId}");
		}

		private bool IsMongoCurrentDatabase()
		{
			try
			{
				var currentDb = _databaseManager?.GetCurrentDatabaseType();
				return currentDb?.Equals("mongo", StringComparison.OrdinalIgnoreCase) == true;
			}
			catch
			{
				return false;
			}
		}

		private async Task<bool> CanAccessDatabase()
		{
			try
			{
				var database = _dynamicClient.GetDatabase();
				if (database == null) return false;

				// Простой ping для проверки соединения
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
					new MongoDB.Bson.BsonDocument("ping", 1),
					cancellationToken: cts.Token);
				return true;
			}
			catch (ObjectDisposedException)
			{
				Log.Warning("MongoDB client is disposed, cannot access database");
				return false;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Cannot access MongoDB database");
				return false;
			}
		}

		private IMongoCollection<T> GetMongoCollection()
		{
			// Проверяем, является ли MongoDB текущей базой данных
			if (!IsMongoCurrentDatabase())
			{
				throw new InvalidOperationException("MongoDB is not the current database");
			}

			try
			{
				var database = _dynamicClient.GetDatabase();
				if (database == null)
				{
					throw new InvalidOperationException("MongoDB database is not available");
				}

				string collectionName = GetCollectionName(typeof(T), _settings.Value);
				var collection = database.GetCollection<T>(collectionName);

				return collection;
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client is disposed, cannot get collection for {EntityType}", typeof(T).Name);
				throw new InvalidOperationException("MongoDB client is disposed", ex);
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to get MongoDB collection for {EntityType}", typeof(T).Name);
				throw new InvalidOperationException($"Failed to access MongoDB collection: {ex.Message}", ex);
			}
		}

		private string GetCollectionName(Type entityType, MongoDbSettings settings)
		{
			return entityType.Name switch
			{
				nameof(OutboxMessage) => settings.Collections.OutboxCollection,
				nameof(IncidentEntity) => settings.Collections.IncidentCollection,
				_ => throw new ArgumentException($"Неизвестный тип {entityType.Name}")
			};
		}

		public async Task<T> GetByIdAsync(string id)
		{
			if (!IsMongoCurrentDatabase())
				return null;

			try
			{
				// Сначала проверяем доступность базы
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for GetByIdAsync");
					return null;
				}

				var collection = GetMongoCollection();
				return await collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync();
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during GetByIdAsync: {Id}", id);
				return null;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to get entity by ID from MongoDB: {Id}", id);
				return null;
			}
		}

		public async Task<IEnumerable<T>> GetAllAsync()
		{
			if (!IsMongoCurrentDatabase())
				return new List<T>();

			try
			{
				// Сначала проверяем доступность базы
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for GetAllAsync");
					return new List<T>();
				}

				var collection = GetMongoCollection();
				return await collection.Find(_ => true).ToListAsync();
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during GetAllAsync");
				return new List<T>();
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to get all entities from MongoDB");
				return new List<T>();
			}
		}

		public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> filter)
		{
			if (!IsMongoCurrentDatabase())
				return new List<T>();

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for FindAsync");
					return new List<T>();
				}

				var collection = GetMongoCollection();
				return await collection.Find(filter).ToListAsync();
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during FindAsync");
				return new List<T>();
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to find entities in MongoDB");
				return new List<T>();
			}
		}

		public async Task InsertAsync(T entity)
		{
			if (!IsMongoCurrentDatabase())
				return;

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for InsertAsync");
					return;
				}

				var collection = GetMongoCollection();
				await collection.InsertOneAsync(entity);
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during InsertAsync");
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to insert entity into MongoDB");
			}
		}

		public async Task UpdateAsync(string id, T updatedEntity)
		{
			if (!IsMongoCurrentDatabase())
				return;

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for UpdateAsync");
					return;
				}

				var collection = GetMongoCollection();
				var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
				var existingEntity = await collection.Find(filter).FirstOrDefaultAsync();

				if (existingEntity == null)
				{
					throw new InvalidOperationException($"Документ с ID {id} не найден");
				}

				var updateDefinitionBuilder = Builders<T>.Update;
				var updates = new List<UpdateDefinition<T>>();

				foreach (var property in typeof(T).GetProperties())
				{
					if (property.Name == "Version") continue;

					var oldValue = property.GetValue(existingEntity);
					var newValue = property.GetValue(updatedEntity);

					if (newValue != null && !newValue.Equals(oldValue))
					{
						updates.Add(updateDefinitionBuilder.Set(property.Name, newValue));
					}
				}

				updates.Add(updateDefinitionBuilder.Set("UpdatedAtUtc", DateTime.UtcNow));
				updates.Add(updateDefinitionBuilder.Inc("Version", 1));

				if (updates.Count > 0)
				{
					var updateDefinition = updateDefinitionBuilder.Combine(updates);
					await collection.UpdateOneAsync(filter, updateDefinition);
				}
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during UpdateAsync");
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to update entity in MongoDB");
			}
		}

		public async Task DeleteByIdAsync(string id)
		{
			if (!IsMongoCurrentDatabase())
				return;

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for DeleteByIdAsync");
					return;
				}

				var collection = GetMongoCollection();
				await collection.DeleteOneAsync(Builders<T>.Filter.Eq("_id", id));
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during DeleteByIdAsync");
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to delete entity from MongoDB");
			}
		}

		public async Task<int> DeleteByTtlAsync(TimeSpan olderThan)
		{
			if (typeof(T) != typeof(OutboxMessage))
			{
				throw new InvalidOperationException("Метод поддерживает только OutboxMessage.");
			}

			if (!IsMongoCurrentDatabase())
				return 0;

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for DeleteByTtlAsync");
					return 0;
				}

				var filter = Builders<OutboxMessage>.Filter.And(
					Builders<OutboxMessage>.Filter.Lt(m => m.CreatedAt, DateTime.UtcNow - olderThan),
					Builders<OutboxMessage>.Filter.Eq(m => m.IsProcessed, true)
				);

				var collection = GetMongoCollection() as IMongoCollection<OutboxMessage>;
				var result = await collection.DeleteManyAsync(filter);

				return (int)result.DeletedCount;
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during DeleteByTtlAsync");
				return 0;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to delete old records from MongoDB");
				return 0;
			}
		}

		public async Task<List<T>> GetUnprocessedMessagesAsync()
		{
			if (!IsMongoCurrentDatabase())
				return new List<T>();

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for GetUnprocessedMessagesAsync");
					return new List<T>();
				}

				var collection = GetMongoCollection();
				var filter = Builders<T>.Filter.Eq("IsProcessed", false);
				return await collection.Find(filter).ToListAsync();
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during GetUnprocessedMessagesAsync");
				return new List<T>();
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to get unprocessed messages from MongoDB");
				return new List<T>();
			}
		}

		public async Task MarkMessageAsProcessedAsync(Guid messageId)
		{
			if (!IsMongoCurrentDatabase())
				return;

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for MarkMessageAsProcessedAsync");
					return;
				}

				var collection = GetMongoCollection();
				var update = Builders<T>.Update
					.Set("IsProcessed", true)
					.Set("ProcessedAt", DateTime.UtcNow);

				await collection.UpdateOneAsync(Builders<T>.Filter.Eq("Id", messageId), update);
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during MarkMessageAsProcessedAsync");
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to mark message as processed in MongoDB");
			}
		}

		public async Task SaveMessageAsync(T message)
		{
			if (!IsMongoCurrentDatabase())
				return;

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for SaveMessageAsync");
					return;
				}

				var collection = GetMongoCollection();
				await collection.InsertOneAsync(message);
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during SaveMessageAsync");
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to save message to MongoDB");
			}
		}

		public async Task UpdateMessageAsync(T message)
		{
			if (!IsMongoCurrentDatabase())
				return;

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for UpdateMessageAsync");
					return;
				}

				if (message is OutboxMessage outboxMessage)
				{
					var collection = GetMongoCollection();
					var filter = Builders<T>.Filter.Eq("Id", outboxMessage.Id);
					var update = Builders<T>.Update
						.Set("IsProcessed", outboxMessage.IsProcessed)
						.Set("ProcessedAt", outboxMessage.ProcessedAt);

					await collection.UpdateOneAsync(filter, update);
				}
				else
				{
					throw new InvalidOperationException("UpdateMessageAsync поддерживает только OutboxMessage");
				}
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during UpdateMessageAsync");
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to update message in MongoDB");
			}
		}

		public async Task UpdateMessagesAsync(IEnumerable<T> messages)
		{
			if (!messages?.Any() ?? true) return;

			if (!IsMongoCurrentDatabase())
				return;

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for UpdateMessagesAsync");
					return;
				}

				var collection = GetMongoCollection();
				var bulkOps = new List<WriteModel<T>>();

				foreach (var message in messages)
				{
					if (message is OutboxMessage outboxMessage)
					{
						var filter = Builders<T>.Filter.Eq("Id", outboxMessage.Id);
						var update = Builders<T>.Update
							.Set("IsProcessed", outboxMessage.IsProcessed)
							.Set("ProcessedAt", outboxMessage.ProcessedAt)
							.Set("UpdatedAtUtc", DateTime.UtcNow);

						bulkOps.Add(new UpdateOneModel<T>(filter, update));
					}
					else
					{
						var messageId = GetMessageId(message);
						var filter = Builders<T>.Filter.Eq("Id", messageId);

						var updateBuilder = Builders<T>.Update;
						var updates = new List<UpdateDefinition<T>>();

						foreach (var property in typeof(T).GetProperties())
						{
							if (property.Name == "Id" || property.Name == "CreatedAt")
								continue;

							var value = property.GetValue(message);
							if (value != null)
							{
								updates.Add(updateBuilder.Set(property.Name, value));
							}
						}

						updates.Add(updateBuilder.Set("UpdatedAtUtc", DateTime.UtcNow));

						if (updates.Any())
						{
							var combinedUpdate = updateBuilder.Combine(updates);
							bulkOps.Add(new UpdateOneModel<T>(filter, combinedUpdate));
						}
					}
				}

				if (bulkOps.Any())
				{
					var options = new BulkWriteOptions { IsOrdered = false };
					var result = await collection.BulkWriteAsync(bulkOps, options);

					Log.Information("MongoDB batch update completed: {ModifiedCount} updated, {UpsertedCount} upserted",
						result.ModifiedCount, result.Upserts.Count);
				}
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during UpdateMessagesAsync");
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to update MongoDB messages, operation skipped");
			}
		}

		private Guid GetMessageId(T message)
		{
			var idProperty = typeof(T).GetProperty("Id");
			if (idProperty != null && idProperty.PropertyType == typeof(Guid))
			{
				return (Guid)idProperty.GetValue(message);
			}
			throw new InvalidOperationException($"Не найдено свойство Id типа Guid в {typeof(T).Name}");
		}

		public async Task InsertMessagesAsync(IEnumerable<T> messages)
		{
			if (messages == null || !messages.Any())
				return;

			if (!IsMongoCurrentDatabase())
				return;

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for InsertMessagesAsync");
					return;
				}

				var collection = GetMongoCollection();
				await collection.InsertManyAsync(messages);
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during InsertMessagesAsync");
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to insert messages into MongoDB");
			}
		}

		public async Task DeleteMessagesAsync(IEnumerable<Guid> ids)
		{
			if (ids == null || !ids.Any())
				return;

			if (!IsMongoCurrentDatabase())
				return;

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for DeleteMessagesAsync");
					return;
				}

				var collection = GetMongoCollection();
				var filter = Builders<T>.Filter.In("Id", ids);
				await collection.DeleteManyAsync(filter);
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during DeleteMessagesAsync");
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to delete messages from MongoDB");
			}
		}

		public async Task<int> DeleteOldRecordsAsync(DateTime cutoffDate, bool requireProcessed = false)
		{
			if (!IsMongoCurrentDatabase())
				return 0;

			try
			{
				if (!await CanAccessDatabase())
				{
					Log.Warning("Cannot access MongoDB database for DeleteOldRecordsAsync");
					return 0;
				}

				var collection = GetMongoCollection();
				var filters = new List<FilterDefinition<T>>
				{
					Builders<T>.Filter.Lt("CreatedAt", cutoffDate.ToUniversalTime())
				};

				if (requireProcessed)
				{
					filters.Add(Builders<T>.Filter.Eq("IsProcessed", true));
				}

				var filter = filters.Count > 1
					? Builders<T>.Filter.And(filters)
					: filters[0];

				var result = await collection.DeleteManyAsync(filter);
				return (int)result.DeletedCount;
			}
			catch (ObjectDisposedException ex)
			{
				Log.Warning(ex, "MongoDB client disposed during DeleteOldRecordsAsync");
				return 0;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to delete old records from MongoDB");
				return 0;
			}
		}
	}
}