﻿using System.Data;
using System.Reflection;
using Dapper;
using lazy_light_requests_gate.core.application.interfaces.databases;
using lazy_light_requests_gate.core.application.interfaces.repos;
using lazy_light_requests_gate.core.domain.entities;
using Npgsql;
using Polly;
using Polly.Retry;
using Serilog;

namespace lazy_light_requests_gate.infrastructure.data.repos;

public class PostgresRepository<T> : IPostgresRepository<T> where T : class
{
	private readonly IDynamicPostgresClient _dynamicClient;
	private readonly string _tableName;
	private readonly AsyncRetryPolicy _retryPolicy;
	private readonly ILogger<PostgresRepository<T>> _logger;

	public PostgresRepository(
		IDynamicPostgresClient dynamicClient,
		ILogger<PostgresRepository<T>> logger)
	{
		_dynamicClient = dynamicClient;
		_logger = logger;
		_tableName = GetTableName(typeof(T));

		_retryPolicy = Policy
			.Handle<NpgsqlException>()
			.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
				onRetry: (exception, timeSpan, retryCount, _) =>
				{
					_logger.LogWarning($"Ошибка PostgreSQL, попытка {retryCount}, повтор через {timeSpan.TotalSeconds} сек. Причина: {exception.Message}");
				});

		try
		{
			using var connection = _dynamicClient.GetConnection();
			_logger.LogInformation($"Успешное подключение к PostgreSQL, таблица: {_tableName}");
		}
		catch (Exception ex)
		{
			_logger.LogError($"Ошибка подключения к PostgreSQL: {ex.Message}");
			throw new InvalidOperationException("Не удалось подключиться к PostgreSQL", ex);
		}
	}

	private string GetTableName(Type type) => type.Name switch
	{
		nameof(OutboxMessage) => "outbox_messages",
		nameof(IncidentEntity) => "incidents",
		_ => throw new ArgumentException($"Неизвестный тип: {type.Name}")
	};

	private IDbConnection CreateConnection() => _dynamicClient.GetConnection();

	public async Task InsertAsync(T entity)
	{
		using var connection = CreateConnection();
		var sql = GenerateInsertSql(entity);
		await _retryPolicy.ExecuteAsync(() => connection.ExecuteAsync(sql, entity));
	}

	public async Task<List<T>> GetUnprocessedMessagesAsync()
	{
		using var connection = CreateConnection();
		var sql = $"SELECT * FROM {_tableName} WHERE is_processed = false";
		var result = await _retryPolicy.ExecuteAsync(() => connection.QueryAsync<T>(sql));
		return result.ToList();
	}

	public async Task SaveMessageAsync(T message) => await InsertAsync(message);

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

		// Исключаем updated_at, так как его нет в таблице
		props = props.Where(p => !string.Equals(p.Name, "UpdatedAt", StringComparison.OrdinalIgnoreCase)).ToList();

		var sets = string.Join(", ", props.Select(p => $"{ToSnakeCase(p.Name)} = @{p.Name}"));

		return $"UPDATE {_tableName} SET {sets} WHERE id = @Id";
	}

	private string ToSnakeCase(string input)
	{
		return string.Concat(input.Select((c, i) =>
			i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));
	}
	public async Task UpdateMessagesAsync(IEnumerable<T> messages)
	{
		if (!messages?.Any() ?? true) return;

		var messagesList = messages.ToList();

		if (typeof(T) == typeof(OutboxMessage))
		{
			await UpdateOutboxMessagesBatch(messagesList.Cast<OutboxMessage>());
		}
		else
		{
			await UpdateMessagesGeneric(messagesList);
		}
	}

	private async Task UpdateOutboxMessagesBatch(IEnumerable<OutboxMessage> messages)
	{
		const string sql = @"
        UPDATE outbox_messages 
        SET 
            is_processed = data.is_processed,
            processed_at = data.processed_at
        FROM (
            SELECT 
                unnest(@Ids) AS id,
                unnest(@IsProcessed) AS is_processed,
                unnest(@ProcessedAt) AS processed_at
        ) AS data
        WHERE outbox_messages.id = data.id";

		var messagesList = messages.ToList();

		var parameters = new
		{
			Ids = messagesList.Select(m => m.Id).ToArray(),
			IsProcessed = messagesList.Select(m => m.IsProcessed).ToArray(),
			ProcessedAt = messagesList.Select(m => m.ProcessedAt).ToArray()
		};

		using var connection = CreateConnection();
		var affectedRows = await _retryPolicy.ExecuteAsync(() =>
			connection.ExecuteAsync(sql, parameters));

		Log.Information("PostgreSQL batch update completed: {AffectedRows} rows updated", affectedRows);
	}

	private async Task UpdateMessagesGeneric(IEnumerable<T> messages)
	{
		using var connection = CreateConnection();
		connection.Open();

		using var transaction = (NpgsqlTransaction)connection.BeginTransaction();

		try
		{
			var updateCount = 0;

			foreach (var message in messages)
			{
				var messageId = GetMessageIdGeneric(message);
				var sql = GenerateUpdateSql(message, messageId);

				var rowsAffected = await connection.ExecuteAsync(sql, message, transaction);
				updateCount += rowsAffected;
			}

			await transaction.CommitAsync();

			Log.Information("PostgreSQL generic batch update completed: {UpdateCount} messages updated", updateCount);
		}
		catch (Exception ex)
		{
			await transaction.RollbackAsync();
			Log.Error(ex, "Error during batch update, transaction rolled back");
			throw;
		}
	}

	private Guid GetMessageIdGeneric(T message)
	{
		var idProperty = typeof(T).GetProperty("Id");
		if (idProperty != null && idProperty.PropertyType == typeof(Guid))
		{
			return (Guid)idProperty.GetValue(message);
		}
		throw new InvalidOperationException($"Не найдено свойство Id типа Guid в {typeof(T).Name}");
	}

	public async Task<int> DeleteOldRecordsAsync(DateTime cutoffDate, bool requireProcessed = false)
	{
		using var connection = CreateConnection();

		try
		{
			string sql;

			// Флаг requireProcessed применяется ТОЛЬКО к OutboxMessage
			if (typeof(T) == typeof(OutboxMessage))
			{
				if (requireProcessed)
				{
					// Удаляем только обработанные сообщения
					sql = $@"
                    DELETE FROM {_tableName} 
                    WHERE created_at < @cutoff 
                    AND is_processed = true";
				}
				else
				{
					// Удаляем все сообщения старше указанной даты
					sql = $@"
                    DELETE FROM {_tableName} 
                    WHERE created_at < @cutoff";
				}
			}
			else
			{
				// Для всех остальных типов (включая IncidentEntity) 
				// флаг requireProcessed игнорируется - удаляем только по дате
				sql = $@"
                DELETE FROM {_tableName} 
                WHERE created_at < @cutoff";
			}

			var deletedCount = await _retryPolicy.ExecuteAsync(() =>
				connection.ExecuteAsync(sql, new { cutoff = cutoffDate }));

			var entityType = typeof(T).Name;
			var processedCondition = typeof(T) == typeof(OutboxMessage) && requireProcessed ? " (только обработанные)" : "";

			_logger.LogInformation(
				"Удалено {Count} старых записей типа {EntityType} из PostgreSQL таблицы {Table} (старше {CutoffDate}){ProcessedCondition}",
				deletedCount, entityType, _tableName, cutoffDate, processedCondition);

			return deletedCount;
		}
		catch (Exception ex)
		{
			var entityType = typeof(T).Name;
			_logger.LogError(ex,
				"Ошибка при удалении старых записей типа {EntityType} из PostgreSQL таблицы {Table}",
				entityType, _tableName);

			return 0;
		}
	}
}
