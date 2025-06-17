using lazy_light_requests_gate.configurationsettings;
using lazy_light_requests_gate.repositories;
using Npgsql;
using System.Data;
using Serilog;
using Dapper;

namespace lazy_light_requests_gate.middleware;

public static class PostgresDbConfiguration
{
	public static IServiceCollection AddPostgresDbServices(this IServiceCollection services, IConfiguration configuration)
	{
		services.Configure<PostgresDbSettings>(configuration.GetSection("PostgresDbSettings"));

		services.AddScoped<IDbConnection>(sp =>
		{
			var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PostgresDbSettings>>().Value;
			var connection = new NpgsqlConnection(settings.GetConnectionString());
			connection.Open();
			return connection;
		});

		services.AddScoped(typeof(IPostgresRepository<>), typeof(PostgresRepository<>));

		return services;
	}

	public static async Task EnsureDatabaseInitializedAsync(IConfiguration configuration)
	{
		var settings = new PostgresDbSettings();
		configuration.GetSection("PostgresDbSettings").Bind(settings);
		var connectionString = settings.GetConnectionString();

		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();

		// Таблицы
		var tableSqlDefinitions = new Dictionary<string, string>
		{
			["outbox_messages"] = @"
				CREATE TABLE IF NOT EXISTS outbox_messages (
					id UUID PRIMARY KEY,
					model_type INT,
					event_type INT,
					is_processed BOOLEAN,
					processed_at TIMESTAMPTZ,
					out_queue TEXT,
					in_queue TEXT,
					payload TEXT,
					routing_key TEXT,
					created_at TIMESTAMPTZ,
					created_at_formatted TEXT,
					source TEXT
				);",

			["queues"] = @"
				CREATE TABLE IF NOT EXISTS queues (
					id UUID PRIMARY KEY,
					created_at_utc TIMESTAMPTZ,
					updated_at_utc TIMESTAMPTZ,
					deleted_at_utc TIMESTAMPTZ,
					created_by TEXT,
					updated_by TEXT,
					deleted_by TEXT,
					is_deleted BOOLEAN,
					version INT,
					ip_address TEXT,
					user_agent TEXT,
					correlation_id TEXT,
					model_type TEXT,
					is_processed BOOLEAN,
					created_at_formatted TEXT,
					in_queue_name TEXT,
					out_queue_name TEXT
				);",

			["incidents"] = @"
				CREATE TABLE IF NOT EXISTS incidents (
					id UUID PRIMARY KEY,
					created_at_utc TIMESTAMPTZ,
					updated_at_utc TIMESTAMPTZ,
					deleted_at_utc TIMESTAMPTZ,
					created_by TEXT,
					updated_by TEXT,
					deleted_by TEXT,
					is_deleted BOOLEAN,
					version INT,
					ip_address TEXT,
					user_agent TEXT,
					correlation_id TEXT,
					model_type TEXT,
					is_processed BOOLEAN,
					created_at_formatted TEXT,
					payload TEXT
				);"
		};

		foreach (var (tableName, createSql) in tableSqlDefinitions)
		{
			try
			{
				await connection.ExecuteAsync(createSql);
				Log.Information("Проверена/создана таблица: {Table}", tableName);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Ошибка при создании таблицы {Table}", tableName);
			}
		}

		// Очереди
		var companyName = configuration["CompanyName"] ?? "default-company";
		var queueIn = $"{companyName.Trim().ToLower()}_in";
		var queueOut = $"{companyName.Trim().ToLower()}_out";

		const string insertQueueSql = @"
			INSERT INTO queues (
				id, created_at_utc, is_deleted, version,
				in_queue_name, out_queue_name
			)
			SELECT @Id, @Now, false, 1, @QueueIn, @QueueOut
			WHERE NOT EXISTS (
				SELECT 1 FROM queues WHERE in_queue_name = @QueueIn AND out_queue_name = @QueueOut
			);";

		try
		{
			await connection.ExecuteAsync(insertQueueSql, new
			{
				Id = Guid.NewGuid(),
				Now = DateTime.UtcNow,
				QueueIn = queueIn,
				QueueOut = queueOut
			});

			Log.Information("Очереди {QueueIn} и {QueueOut} проверены/созданы в таблице queues:", queueIn, queueOut);
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Не удалось вставить название очередей {QueueIn}/{QueueOut}", queueIn, queueOut);
		}
	}
}
