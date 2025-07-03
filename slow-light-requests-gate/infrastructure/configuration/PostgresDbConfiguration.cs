using Dapper;
using lazy_light_requests_gate.core.application.interfaces.common;
using lazy_light_requests_gate.core.application.interfaces.repos;
using lazy_light_requests_gate.core.application.services.common;
using lazy_light_requests_gate.infrastructure.data.repos;
using lazy_light_requests_gate.presentation.models.settings.databases;
using Npgsql;
using Serilog;

namespace lazy_light_requests_gate.infrastructure.configuration;

/// <summary>
/// Единая конфигурация PostgreSQL - объединяет все функции в одном классе
/// </summary>
public static class PostgresDbConfiguration
{
	/// <summary>
	/// Регистрирует все сервисы PostgreSQL в DI контейнере
	/// </summary>
	public static IServiceCollection AddPostgresDbServices(this IServiceCollection services, IConfiguration configuration)
	{
		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

		// Получаем секцию настроек PostgreSQL
		var postgresSection = configuration.GetSection("PostgresDbSettings");

		// Проверяем, что секция существует
		if (!postgresSection.Exists())
		{
			Console.WriteLine($"[{timestamp}] [WARNING] PostgresDbSettings не найден в конфигурации");
			return services;
		}

		// Регистрируем настройки
		services.Configure<PostgresDbSettings>(postgresSection);

		// Получаем значения для отладки - используем более надежный способ
		var settings = postgresSection.Get<PostgresDbSettings>();

		// Дополнительная отладка
		Console.WriteLine($"[{timestamp}] [DEBUG] PostgreSQL конфигурация (через .Get<>()):");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Host: '{settings?.Host ?? "NULL"}'");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Port: '{settings?.Port.ToString() ?? "NULL"}'");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Username: '{settings?.Username ?? "NULL"}'");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Password: '{(string.IsNullOrEmpty(settings?.Password) ? "NULL/EMPTY" : $"SET (length: {settings.Password.Length})")}'");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Database: '{settings?.Database ?? "NULL"}'");

		// Также проверим через GetValue для сравнения
		var host = postgresSection.GetValue<string>("Host");
		var port = postgresSection.GetValue<string>("Port");
		var username = postgresSection.GetValue<string>("Username");
		var password = postgresSection.GetValue<string>("Password");
		var database = postgresSection.GetValue<string>("Database");

		Console.WriteLine($"[{timestamp}] [DEBUG] PostgreSQL конфигурация (через GetValue):");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Host: '{host ?? "NULL"}'");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Port: '{port ?? "NULL"}'");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Username: '{username ?? "NULL"}'");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Password: '{(string.IsNullOrEmpty(password) ? "NULL/EMPTY" : $"SET (length: {password.Length})")}'");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Database: '{database ?? "NULL"}'");

		// Проверяем обязательные поля
		if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) ||
			string.IsNullOrEmpty(password) || string.IsNullOrEmpty(database))
		{
			throw new InvalidOperationException("Все поля PostgreSQL конфигурации обязательны для заполнения");
		}

		// Формируем строку подключения более безопасным способом
		var connectionStringBuilder = new NpgsqlConnectionStringBuilder
		{
			Host = host,
			Port = int.TryParse(port, out var parsedPort) ? parsedPort : 5432,
			Username = username,
			Password = password,
			Database = database
		};

		var connectionString = connectionStringBuilder.ToString();
		Console.WriteLine($"[{timestamp}] [DEBUG] - ConnectionString сформирован (длина: {connectionString.Length})");

		// Отладочная информация о строке подключения (без пароля)
		var debugConnectionString = connectionString.Replace(password, "***");
		Console.WriteLine($"[{timestamp}] [DEBUG] - ConnectionString: {debugConnectionString}");

		// Регистрируем фабрику подключений
		services.AddSingleton<IPostgresConnectionFactory>(provider =>
			new PostgresConnectionFactory(connectionString));

		// Регистрируем IDbConnection для Dapper (Scoped - новое подключение для каждого запроса)
		services.AddScoped(sp =>
		{
			var factory = sp.GetRequiredService<IPostgresConnectionFactory>();
			var connection = factory.CreateConnection();
			connection.Open();
			return connection;
		});

		// Регистрируем репозитории PostgreSQL
		services.AddScoped(typeof(IPostgresRepository<>), typeof(PostgresRepository<>));

		Console.WriteLine($"[{timestamp}] [SUCCESS] PostgreSQL сервисы успешно зарегистрированы");
		return services;
	}

	/// <summary>
	/// Инициализирует базу данных PostgreSQL (создает таблицы, заполняет начальные данные)
	/// </summary>
	public static async Task EnsureDatabaseInitializedAsync(IConfiguration configuration)
	{
		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
		Console.WriteLine($"[{timestamp}] [INFO] Начинается инициализация PostgreSQL базы данных...");

		try
		{
			// Получаем настройки напрямую из конфигурации
			var postgresSection = configuration.GetSection("PostgresDbSettings");
			var settings = postgresSection.Get<PostgresDbSettings>();

			if (settings == null)
			{
				throw new InvalidOperationException("Не удалось получить настройки PostgreSQL из конфигурации");
			}

			// Дополнительная проверка настроек
			Console.WriteLine($"[{timestamp}] [DEBUG] Проверка настроек для инициализации:");
			Console.WriteLine($"[{timestamp}] [DEBUG] - Host: '{settings.Host ?? "NULL"}'");
			Console.WriteLine($"[{timestamp}] [DEBUG] - Database: '{settings.Database ?? "NULL"}'");
			Console.WriteLine($"[{timestamp}] [DEBUG] - Username: '{settings.Username ?? "NULL"}'");
			Console.WriteLine($"[{timestamp}] [DEBUG] - Password: '{(string.IsNullOrEmpty(settings.Password) ? "НЕ УСТАНОВЛЕН" : $"УСТАНОВЛЕН (длина: {settings.Password.Length})")}'");

			// ИСПРАВЛЕНИЕ: Сначала создаем базу данных, а потом уже подключаемся к ней
			await CreateDataBaseAndTablesAsync(configuration);

			Console.WriteLine($"[{timestamp}] [SUCCESS] Инициализация PostgreSQL завершена успешно");
		}
		catch (Exception ex)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"[{timestamp}] [ERROR] Ошибка инициализации PostgreSQL: {ex.Message}");
			Console.WriteLine($"[{timestamp}] [ERROR] Тип исключения: {ex.GetType().Name}");
			if (ex.InnerException != null)
			{
				Console.WriteLine($"[{timestamp}] [ERROR] Внутреннее исключение: {ex.InnerException.Message}");
			}
			Console.ResetColor();
			throw;
		}
	}

	/// <summary>
	/// Создает необходимые таблицы в базе данных
	/// </summary>
	private static async Task CreateDataBaseAndTablesAsync(IConfiguration configuration)
	{
		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
		Console.WriteLine($"[{timestamp}] [INFO] Проверка и создание базы PostgreSQL...");

		var username = configuration["PostgresDbSettings:Username"] ?? "postgres";
		var databaseName = configuration["PostgresDbSettings:Database"] ?? "GatewayDB";
		var password = configuration["PostgresDbSettings:Password"];
		var host = configuration["PostgresDbSettings:Host"];
		var port = configuration["PostgresDbSettings:Port"];

		Console.WriteLine($"[{timestamp}] [DEBUG] Параметры для создания базы:");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Host: '{host}'");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Port: '{port}'");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Username: '{username}'");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Password: '{(string.IsNullOrEmpty(password) ? "НЕ УСТАНОВЛЕН" : "УСТАНОВЛЕН")}'");
		Console.WriteLine($"[{timestamp}] [DEBUG] - Database: '{databaseName}'");

		// ШАГ 1: Подключаемся к системной базе postgres и создаем нашу базу
		var adminConnectionStringBuilder = new NpgsqlConnectionStringBuilder();

		adminConnectionStringBuilder.Host = host;
		adminConnectionStringBuilder.Port = int.TryParse(port, out var parsedPort) ? parsedPort : 5432;
		adminConnectionStringBuilder.Username = username;
		adminConnectionStringBuilder.Password = password;
		adminConnectionStringBuilder.Database = "postgres";

		var adminConnectionString = adminConnectionStringBuilder.ToString();
		Console.WriteLine($"[{timestamp}] [DEBUG] - Admin connection string построен (длина: {adminConnectionString.Length})");

		await using var adminConnection = new NpgsqlConnection(adminConnectionString);

		Console.WriteLine($"[{timestamp}] [DEBUG] Попытка подключения к системной базе postgres...");
		await adminConnection.OpenAsync();
		Console.WriteLine($"[{timestamp}] [SUCCESS] Подключение к системной базе postgres установлено");

		// Проверяем существование базы
		var checkDbSql = "SELECT 1 FROM pg_database WHERE datname = @DatabaseName";
		var dbExists = await adminConnection.ExecuteScalarAsync<int?>(checkDbSql, new { DatabaseName = databaseName });

		if (dbExists != 1)
		{
			Console.WriteLine($"[{timestamp}] [INFO] База данных '{databaseName}' не существует. Создаём...");

			var terminateConnectionsSql = @"
				SELECT pg_terminate_backend(pid)
				FROM pg_stat_activity
				WHERE datname = @DatabaseName AND pid <> pg_backend_pid();";

			await adminConnection.ExecuteAsync(terminateConnectionsSql, new { DatabaseName = databaseName });

			var createDbSql = $@"CREATE DATABASE ""{databaseName}"" WITH OWNER = ""{username}"";";
			await adminConnection.ExecuteAsync(createDbSql);

			Console.WriteLine($"[{timestamp}] [SUCCESS] База данных '{databaseName}' успешно создана.");
		}
		else
		{
			Console.WriteLine($"[{timestamp}] [INFO] База данных '{databaseName}' уже существует. Пропускаем создание.");
		}

		// ШАГ 2: Теперь подключаемся к нашей базе и создаем таблицы
		var targetConnectionStringBuilder = new NpgsqlConnectionStringBuilder
		{
			Host = host,
			Port = int.TryParse(port, out var targetPort) ? targetPort : 5432,
			Username = username,
			Password = password,
			Database = databaseName  // Теперь подключаемся к нашей базе
		};

		var targetConnectionString = targetConnectionStringBuilder.ToString();

		await using var targetConnection = new NpgsqlConnection(targetConnectionString);
		Console.WriteLine($"[{timestamp}] [DEBUG] Подключение к целевой базе '{databaseName}'...");
		await targetConnection.OpenAsync();
		Console.WriteLine($"[{timestamp}] [SUCCESS] Подключение к целевой базе '{databaseName}' установлено");

		// ШАГ 3: Создаем таблицы и схему
		var fullSql = @"
			CREATE OR REPLACE FUNCTION notify_outbox_message()
			RETURNS TRIGGER AS $$
			BEGIN
				PERFORM pg_notify('outbox_new_message', NEW.id::text);
				RETURN NEW;
			END;
			$$ LANGUAGE plpgsql;

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
				source TEXT,
				retry_count INTEGER DEFAULT NULL,
				last_error TEXT DEFAULT NULL,
				last_retry_at TIMESTAMP DEFAULT NULL,
				max_retries INTEGER DEFAULT 3,
				is_dead_letter BOOLEAN DEFAULT FALSE
			);

			CREATE TABLE IF NOT EXISTS incidents (
				id UUID PRIMARY KEY,
				created_at TIMESTAMPTZ,
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
			);

			CREATE INDEX IF NOT EXISTS idx_outbox_retry_lookup 
				ON outbox_messages(is_processed, is_dead_letter, retry_count);

			CREATE INDEX IF NOT EXISTS idx_outbox_cleanup 
				ON outbox_messages(is_processed, processed_at);

			CREATE INDEX IF NOT EXISTS idx_incidents_cleanup 
				ON incidents(created_at);

			DROP TRIGGER IF EXISTS outbox_message_notify_trigger ON outbox_messages;

			CREATE TRIGGER outbox_message_notify_trigger
				AFTER INSERT ON outbox_messages
				FOR EACH ROW
				EXECUTE FUNCTION notify_outbox_message();
		";

		try
		{
			Console.WriteLine($"[{timestamp}] [INFO] Создание таблиц и схемы...");
			await VerifyTriggersAsync(targetConnection);
			await targetConnection.ExecuteAsync(fullSql);
			Log.Information("База данных {DatabaseName} и все таблицы созданы успешно", databaseName);
			Console.WriteLine($"[{timestamp}] [SUCCESS] Таблицы и схема созданы успешно");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Ошибка при создании таблиц в базе данных {DatabaseName}", databaseName);
			Console.WriteLine($"[{timestamp}] [ERROR] Ошибка создания таблиц: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Проверка созданных триггеров
	/// </summary>
	private static async Task VerifyTriggersAsync(NpgsqlConnection connection)
	{
		try
		{
			const string verificationSql = @"
                SELECT 
                    trigger_name, 
                    event_manipulation, 
                    event_object_table,
                    action_statement
                FROM information_schema.triggers 
                WHERE event_object_table = 'outbox_messages';";

			var triggers = await connection.QueryAsync(verificationSql);

			if (triggers.Any())
			{
				Log.Information("Найдены триггеры для outbox_messages: {Count}", triggers.Count());
				foreach (var trigger in triggers)
				{
					Log.Information("Триггер: {Name} на {Event}", trigger.trigger_name, trigger.event_manipulation);
				}
			}
			else
			{
				Log.Warning("Триггеры для outbox_messages не найдены");
			}
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Ошибка при проверке триггеров");
		}
	}

	/// <summary>
	/// Диагностический метод для проверки состояния базы данных
	/// </summary>
	public static async Task DiagnoseDatabaseAsync(IConfiguration configuration)
	{
		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
		Console.WriteLine($"[{timestamp}] [INFO] === НАЧАЛО ДИАГНОСТИКИ БАЗЫ ДАННЫХ ===");

		try
		{
			var postgresSection = configuration.GetSection("PostgresDbSettings");
			var settings = postgresSection.Get<PostgresDbSettings>();

			// Сначала проверяем, существует ли база данных
			var adminConnectionStringBuilder = new NpgsqlConnectionStringBuilder
			{
				Host = settings.Host,
				Port = settings.Port,
				Username = settings.Username,
				Password = settings.Password,
				Database = "postgres"  // Подключаемся к системной базе для проверки
			};

			await using var adminConnection = new NpgsqlConnection(adminConnectionStringBuilder.ToString());
			await adminConnection.OpenAsync();

			var checkDbQuery = "SELECT COUNT(*) FROM pg_database WHERE datname = @DatabaseName";
			var dbExists = await adminConnection.QuerySingleAsync<int>(checkDbQuery, new { DatabaseName = settings.Database });

			Console.WriteLine($"[{timestamp}] [INFO] База данных '{settings.Database}' существует: {dbExists > 0}");

			if (dbExists == 0)
			{
				Console.WriteLine($"[{timestamp}] [WARNING] База данных не найдена. Диагностика невозможна.");
				return;
			}

			// Теперь подключаемся к нашей базе для диагностики
			var connectionStringBuilder = new NpgsqlConnectionStringBuilder
			{
				Host = settings.Host,
				Port = settings.Port,
				Username = settings.Username,
				Password = settings.Password,
				Database = settings.Database
			};

			await using var connection = new NpgsqlConnection(connectionStringBuilder.ToString());
			await connection.OpenAsync();

			Console.WriteLine($"[{timestamp}] [SUCCESS] Подключение к базе {settings.Database} установлено");

			// 1. Проверяем список таблиц
			var tablesQuery = @"
            SELECT table_name, table_type 
            FROM information_schema.tables 
            WHERE table_schema = 'public' 
            ORDER BY table_name";

			var tables = await connection.QueryAsync<dynamic>(tablesQuery);
			Console.WriteLine($"[{timestamp}] [INFO] Найдено таблиц: {tables.Count()}");

			foreach (var table in tables)
			{
				Console.WriteLine($"[{timestamp}] [INFO] - Таблица: {table.table_name} (тип: {table.table_type})");
			}

			// 2. Проверяем конкретные таблицы
			var targetTables = new[] { "outbox_messages", "incidents" };

			foreach (var tableName in targetTables)
			{
				var tableExistsQuery = @"
                SELECT EXISTS (
                    SELECT FROM information_schema.tables 
                    WHERE table_schema = 'public' 
                    AND table_name = @TableName
                )";

				var exists = await connection.QuerySingleAsync<bool>(tableExistsQuery, new { TableName = tableName });
				Console.WriteLine($"[{timestamp}] [INFO] Таблица '{tableName}' существует: {exists}");

				if (exists)
				{
					// Проверяем количество записей
					var countQuery = $"SELECT COUNT(*) FROM {tableName}";
					var count = await connection.QuerySingleAsync<int>(countQuery);
					Console.WriteLine($"[{timestamp}] [INFO] - Записей в '{tableName}': {count}");

					// Проверяем колонки
					var columnsQuery = @"
                    SELECT column_name, data_type, is_nullable
                    FROM information_schema.columns
                    WHERE table_name = @TableName
                    AND table_schema = 'public'
                    ORDER BY ordinal_position";

					var columns = await connection.QueryAsync<dynamic>(columnsQuery, new { TableName = tableName });
					Console.WriteLine($"[{timestamp}] [INFO] - Колонок в '{tableName}': {columns.Count()}");

					foreach (var column in columns)
					{
						Console.WriteLine($"[{timestamp}] [DEBUG]   * {column.column_name}: {column.data_type} (nullable: {column.is_nullable})");
					}
				}
			}

			// 3. Проверяем триггеры
			var triggersQuery = @"
            SELECT trigger_name, event_object_table, event_manipulation
            FROM information_schema.triggers
            WHERE event_object_table IN ('outbox_messages', 'incidents')
            ORDER BY event_object_table, trigger_name";

			var triggers = await connection.QueryAsync<dynamic>(triggersQuery);
			Console.WriteLine($"[{timestamp}] [INFO] Найдено триггеров: {triggers.Count()}");

			foreach (var trigger in triggers)
			{
				Console.WriteLine($"[{timestamp}] [INFO] - Триггер: {trigger.trigger_name} на {trigger.event_object_table} ({trigger.event_manipulation})");
			}

			// 4. Проверяем функции
			var functionsQuery = @"
            SELECT routine_name, routine_type
            FROM information_schema.routines
            WHERE routine_schema = 'public'
            AND routine_name LIKE '%outbox%'
            ORDER BY routine_name";

			var functions = await connection.QueryAsync<dynamic>(functionsQuery);
			Console.WriteLine($"[{timestamp}] [INFO] Найдено функций (outbox): {functions.Count()}");

			foreach (var function in functions)
			{
				Console.WriteLine($"[{timestamp}] [INFO] - Функция: {function.routine_name} (тип: {function.routine_type})");
			}

			// 5. Проверяем индексы
			var indexesQuery = @"
            SELECT indexname, tablename
            FROM pg_indexes
            WHERE tablename IN ('outbox_messages', 'incidents')
            ORDER BY tablename, indexname";

			var indexes = await connection.QueryAsync<dynamic>(indexesQuery);
			Console.WriteLine($"[{timestamp}] [INFO] Найдено индексов: {indexes.Count()}");

			foreach (var index in indexes)
			{
				Console.WriteLine($"[{timestamp}] [INFO] - Индекс: {index.indexname} на {index.tablename}");
			}

			// 6. Тест вставки и удаления записи
			Console.WriteLine($"[{timestamp}] [INFO] Тестируем вставку данных...");

			var testId = Guid.NewGuid();
			var insertTestQuery = @"
            INSERT INTO outbox_messages (
                id, model_type, event_type, is_processed, payload, 
                created_at, retry_count, max_retries, is_dead_letter
            ) VALUES (
                @Id, 1, 1, false, 'test payload', 
                NOW(), 0, 3, false
            )";

			await connection.ExecuteAsync(insertTestQuery, new { Id = testId });
			Console.WriteLine($"[{timestamp}] [SUCCESS] Тестовая запись вставлена");

			// Проверяем, что запись есть
			var selectTestQuery = "SELECT COUNT(*) FROM outbox_messages WHERE id = @Id";
			var testCount = await connection.QuerySingleAsync<int>(selectTestQuery, new { Id = testId });
			Console.WriteLine($"[{timestamp}] [INFO] Тестовая запись найдена: {testCount > 0}");

			// Удаляем тестовую запись
			var deleteTestQuery = "DELETE FROM outbox_messages WHERE id = @Id";
			await connection.ExecuteAsync(deleteTestQuery, new { Id = testId });
			Console.WriteLine($"[{timestamp}] [SUCCESS] Тестовая запись удалена");

			Console.WriteLine($"[{timestamp}] [SUCCESS] === ДИАГНОСТИКА ЗАВЕРШЕНА УСПЕШНО ===");
		}
		catch (Exception ex)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"[{timestamp}] [ERROR] Ошибка диагностики: {ex.Message}");
			Console.WriteLine($"[{timestamp}] [ERROR] Тип исключения: {ex.GetType().Name}");
			if (ex.InnerException != null)
			{
				Console.WriteLine($"[{timestamp}] [ERROR] Внутреннее исключение: {ex.InnerException.Message}");
			}
			Console.ResetColor();
		}
	}
}