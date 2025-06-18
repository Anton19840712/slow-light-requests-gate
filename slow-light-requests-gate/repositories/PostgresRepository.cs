using System.Data;
using System.Reflection;
using Dapper;
using lazy_light_requests_gate.configurationsettings;
using lazy_light_requests_gate.entities;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;
using Polly.Retry;
using Serilog;

namespace lazy_light_requests_gate.repositories;

public class PostgresRepository<T> : IPostgresRepository<T> where T : class
{
	private readonly string _connectionString;
	private readonly string _tableName;
	private readonly AsyncRetryPolicy _retryPolicy;

	public PostgresRepository(IOptions<PostgresDbSettings> settings)
	{
		_connectionString = settings.Value.GetConnectionString();
		_tableName = GetTableName(typeof(T));
		_retryPolicy = Policy
			.Handle<NpgsqlException>()
			.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
				onRetry: (exception, timeSpan, retryCount, _) =>
				{
					Log.Warning($"Ошибка PostgreSQL, попытка {retryCount}, повтор через {timeSpan.TotalSeconds} сек. Причина: {exception.Message}");
				});

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			connection.Open();
			Log.Information($"Успешное подключение к PostgreSQL, таблица: {_tableName}");
		}
		catch (Exception ex)
		{
			Log.Error($"Ошибка подключения к PostgreSQL: {ex.Message}");
			throw new InvalidOperationException("Не удалось подключиться к PostgreSQL", ex);
		}
	}

	private string GetTableName(Type type) => type.Name switch
	{
		nameof(OutboxMessage) => "outbox_messages",
		nameof(QueuesEntity) => "queues",
		nameof(IncidentEntity) => "incidents",
		_ => throw new ArgumentException($"Неизвестный тип: {type.Name}")
	};

	private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

	public async Task<T> GetByIdAsync(Guid id)
	{
		using var connection = CreateConnection();
		var sql = $"SELECT * FROM {_tableName} WHERE id = @id";
		return await _retryPolicy.ExecuteAsync(() => connection.QueryFirstOrDefaultAsync<T>(sql, new { id }));
	}

	public async Task<IEnumerable<T>> GetAllAsync()
	{
		using var connection = CreateConnection();
		var sql = $"SELECT * FROM {_tableName}";
		return await _retryPolicy.ExecuteAsync(() => connection.QueryAsync<T>(sql));
	}

	public async Task<IEnumerable<T>> FindAsync(string whereClause, object parameters = null)
	{
		using var connection = CreateConnection();
		var sql = $"SELECT * FROM {_tableName} WHERE {whereClause}";
		return await _retryPolicy.ExecuteAsync(() => connection.QueryAsync<T>(sql, parameters));
	}

	public async Task InsertAsync(T entity)
	{
		using var connection = CreateConnection();
		var sql = GenerateInsertSql(entity);
		await _retryPolicy.ExecuteAsync(() => connection.ExecuteAsync(sql, entity));
	}

	public async Task UpdateAsync(Guid id, T updatedEntity)
	{
		using var connection = CreateConnection();
		var sql = GenerateUpdateSql(updatedEntity, id);
		await _retryPolicy.ExecuteAsync(() => connection.ExecuteAsync(sql, updatedEntity));
	}

	public async Task DeleteByIdAsync(Guid id)
	{
		using var connection = CreateConnection();
		var sql = $"DELETE FROM {_tableName} WHERE id = @id";
		await _retryPolicy.ExecuteAsync(() => connection.ExecuteAsync(sql, new { id }));
	}

	public async Task<int> DeleteByTtlAsync(TimeSpan olderThan)
	{
		if (typeof(T) != typeof(OutboxMessage))
			throw new InvalidOperationException("Метод поддерживает только OutboxMessage");

		using var connection = CreateConnection();
		var cutoffTime = DateTime.UtcNow - olderThan;
		var sql = $"DELETE FROM {_tableName} WHERE created_at < @cutoff AND is_processed = true";

		Log.Information("PostgreSQL DeleteByTtlAsync: попытка удаления сообщений старше {CutoffTime} из таблицы {TableName}",
			cutoffTime, _tableName);

		var deletedCount = await _retryPolicy.ExecuteAsync(() =>
			connection.ExecuteAsync(sql, new { cutoff = cutoffTime }));

		Log.Information("PostgreSQL DeleteByTtlAsync: удалено {DeletedCount} старых сообщений из {TableName}",
			deletedCount, _tableName);

		return deletedCount;
	}

	public async Task<List<T>> GetUnprocessedMessagesAsync()
	{
		using var connection = CreateConnection();
		var sql = $"SELECT * FROM {_tableName} WHERE is_processed = false";
		var result = await _retryPolicy.ExecuteAsync(() => connection.QueryAsync<T>(sql));
		return result.ToList();
	}

	public async Task MarkMessageAsProcessedAsync(Guid messageId)
	{
		using var connection = CreateConnection();
		var sql = $@"
		UPDATE {_tableName}
		SET is_processed = true,
			processed_at = @now
		WHERE id = @messageId";

		await _retryPolicy.ExecuteAsync(() =>
			connection.ExecuteAsync(sql, new { messageId, now = DateTime.UtcNow }));
	}

	public async Task<int> DeleteOldMessagesAsync(TimeSpan olderThanFromNow)
	{
		using var connection = CreateConnection();

		// cutoffTime — время, до которого считаем записи устаревшими
		var cutoffTime = DateTime.UtcNow - olderThanFromNow;
		Log.Information("cutoffTime (UTC): {CutoffTime}", cutoffTime);

		// Подсчёт кандидатов на удаление
		const string countSql = @"
		SELECT COUNT(*) 
		FROM outbox_messages 
		WHERE created_at < @cutoff AND is_processed = true";

		var candidatesCount = await connection.QuerySingleAsync<int>(countSql, new { cutoff = cutoffTime });

		Log.Information("PostgreSQL DeleteOldMessagesAsync: найдено {CandidatesCount} сообщений-кандидатов для удаления (созданы до {CutoffTime})",
			candidatesCount, cutoffTime);

		// Удаление записей
		const string deleteSql = @"
		DELETE FROM outbox_messages 
		WHERE created_at < @cutoff AND is_processed = true";

		var deletedCount = await connection.ExecuteAsync(deleteSql, new { cutoff = cutoffTime });

		Log.Information("PostgreSQL DeleteOldMessagesAsync: удалено {DeletedCount} из {CandidatesCount} старых сообщений из outbox_messages",
			deletedCount, candidatesCount);

		return deletedCount;
	}


	public async Task SaveMessageAsync(T message) => await InsertAsync(message);

	public async Task UpdateMessageAsync(T message)
	{
		if (message is OutboxMessage outboxMessage)
		{
			const string sql = @"
				UPDATE outbox_messages 
				SET is_processed = @IsProcessed, processed_at = @ProcessedAt 
				WHERE id = @Id";

			using var connection = CreateConnection();
			await _retryPolicy.ExecuteAsync(() => connection.ExecuteAsync(sql, outboxMessage));
		}
		else
		{
			throw new InvalidOperationException("UpdateMessageAsync поддерживает только OutboxMessage");
		}
	}

	private string GenerateInsertSql(T entity)
	{
		var props = typeof(T).GetProperties()
			.Where(p => !IsNotMapped(p))
			.ToList();

		var columns = string.Join(", ", props.Select(p => ToSnakeCase(p.Name)));
		var values = string.Join(", ", props.Select(p => "@" + p.Name));

		return $"INSERT INTO {_tableName} ({columns}) VALUES ({values})";
	}

	private bool IsNotMapped(PropertyInfo prop) =>
		Attribute.IsDefined(prop, typeof(System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute));

	private string GenerateUpdateSql(T entity, Guid id)
	{
		var props = typeof(T).GetProperties()
			.Where(p => !IsNotMapped(p))
			.ToList();

		var sets = string.Join(", ", props.Select(p => $"{ToSnakeCase(p.Name)} = @{p.Name}"));

		return $"UPDATE {_tableName} SET {sets}, updated_at = @UpdatedAtUtc WHERE id = @Id";
	}

	private string ToSnakeCase(string input)
	{
		return string.Concat(input.Select((c, i) =>
			i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));
	}
}
