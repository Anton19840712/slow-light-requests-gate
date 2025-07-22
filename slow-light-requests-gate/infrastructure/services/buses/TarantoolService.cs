using lazy_light_requests_gate.core.application.interfaces.buses;
using ProGaudi.Tarantool.Client;
using ProGaudi.Tarantool.Client.Model;
using ProGaudi.Tarantool.Client.Model.Enums;
using System.Text.Json;

namespace lazy_light_requests_gate.infrastructure.services.buses
{
	/// <summary>
	/// Сервис для работы с Tarantool
	/// </summary>
	public class TarantoolService : ITarantoolService, IDisposable, IAsyncDisposable
	{
		private readonly ILogger<TarantoolService> _logger;
		private readonly IConfiguration _configuration;
		private readonly string _host;
		private readonly int _port;
		private readonly string _username;
		private readonly string _password;
		private readonly string _inputSpace;
		private readonly string _outputSpace;
		private readonly string _streamName;
		private IBox _box;
		private ISpace _inputSpaceHandle;
		private ISpace _outputSpaceHandle;
		private readonly SemaphoreSlim _semaphore = new(1, 1);
		private volatile bool _disposed = false;
		private volatile bool _initialized = false;
		private readonly object _disposeLock = new object();

		public TarantoolService(IConfiguration configuration, ILogger<TarantoolService> logger = null)
		{
			_logger = logger;
			_configuration = configuration;

			// Читаем настройки прямо из конфигурации
			_host = _configuration["TarantoolSettings:Host"] ?? "localhost";
			_port = int.TryParse(_configuration["TarantoolSettings:Port"], out var port) ? port : 3301;
			_username = _configuration["TarantoolSettings:Username"] ?? "";
			_password = _configuration["TarantoolSettings:Password"] ?? "";
			_inputSpace = _configuration["TarantoolSettings:InputSpace"] ?? "messages_in";
			_outputSpace = _configuration["TarantoolSettings:OutputSpace"] ?? "messages_out";
			_streamName = _configuration["TarantoolSettings:StreamName"] ?? "default-stream";

			_logger?.LogDebug("TarantoolService initialized with config: Host={Host}:{Port}, InputSpace={InputSpace}, OutputSpace={OutputSpace}, Stream={Stream}",
				_host, _port, _inputSpace, _outputSpace, _streamName);

			// НЕ инициализируем соединение в конструкторе - делаем это лениво
			_logger?.LogInformation("TarantoolService created. Connection will be initialized on first use.");
		}

		private async Task EnsureInitializedAsync()
		{
			if (_initialized && _box != null)
				return;

			await _semaphore.WaitAsync();
			try
			{
				if (_initialized && _box != null)
					return;

				_logger?.LogDebug("Initializing Tarantool connection...");
				await InitializeConnectionAsync();
				_initialized = true;
				_logger?.LogInformation("Tarantool connection initialized successfully");
			}
			finally
			{
				_semaphore.Release();
			}
		}

		private async Task InitializeConnectionAsync()
		{
			try
			{
				// Очищаем предыдущее соединение если есть
				_box?.Dispose();

				// Connect with authentication if username/password are provided
				if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
				{
					_logger?.LogDebug("Connecting to Tarantool with authentication: {Username}", _username);

					// Create connection string with authentication in tarantool:// format
					var connectionString = $"tarantool://{_username}:{_password}@{_host}:{_port}";
					_box = await Box.Connect(connectionString);
				}
				else
				{
					_logger?.LogDebug("Connecting to Tarantool as guest user (no credentials provided)");
					var connectionString = $"{_host}:{_port}";
					_box = await Box.Connect(connectionString);
				}

				// Создаем spaces простым способом
				await EnsureSpacesExistAsync();

				// ВАЖНО: Обновляем схему после создания spaces
				await _box.ReloadSchema();

				// Получаем handles для spaces
				try
				{
#pragma warning disable CS0618 // Type or member is obsolete
					var schema = _box.GetSchema();
#pragma warning restore CS0618 // Type or member is obsolete

					_inputSpaceHandle = schema[_inputSpace];
					_outputSpaceHandle = schema[_outputSpace];

					_logger?.LogInformation("Successfully connected to Tarantool spaces: {InputSpace}, {OutputSpace}",
						_inputSpace, _outputSpace);
				}
				catch (Exception ex)
				{
					_logger?.LogError(ex, "Could not get space handles: {Error}. Please create spaces manually in Tarantool console.", ex.Message);
					_logger?.LogInformation("To create spaces manually, run in Tarantool console:\n" +
						"box.schema.space.create('{InputSpace}')\n" +
						"box.space.{InputSpace}:create_index('primary', {{sequence = true}})\n" +
						"box.schema.space.create('{OutputSpace}')\n" +
						"box.space.{OutputSpace}:create_index('primary', {{sequence = true}})",
						_inputSpace, _inputSpace, _outputSpace, _outputSpace);
					throw;
				}
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Failed to initialize Tarantool client with Host: {Host}:{Port}, Username: {Username}",
					_host, _port, _username);
				throw;
			}
		}

		/// <summary>
		/// Создаем spaces из кода
		/// </summary>
		/// <returns></returns>
		private async Task EnsureSpacesExistAsync()
		{
			try
			{
				_logger?.LogInformation("Ensuring Tarantool spaces exist...");

				// Создаем Input Space
				await CreateSpaceIfNotExists(_inputSpace);

				// Создаем Output Space  
				await CreateSpaceIfNotExists(_outputSpace);

				_logger?.LogInformation("Tarantool spaces initialization completed successfully");
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Failed to ensure Tarantool spaces exist");
				throw;
			}
		}

		private async Task CreateSpaceIfNotExists(string spaceName)
		{
			try
			{
				_logger?.LogInformation("Attempting to create space '{SpaceName}'...", spaceName);

				// Правильный Lua скрипт с форматом полей
				var luaScript = $@"
					local space_name = '{spaceName}'
					local space = box.space[space_name]
					
					if space == nil then
						space = box.schema.space.create(space_name, {{
							format = {{
								{{name = 'id', type = 'unsigned'}},
								{{name = 'message', type = 'string'}}, 
								{{name = 'timestamp', type = 'string'}},
								{{name = 'stream', type = 'string'}}
							}}
						}})
						
						space:create_index('primary', {{
							parts = {{1}},
							sequence = true
						}})
						
						print('Space ' .. space_name .. ' created successfully with format')
					else
						print('Space ' .. space_name .. ' already exists')
					end
				";

				// Выполняем с явным типом
				await _box.Eval<TarantoolTuple<string>>(luaScript);

				_logger?.LogInformation("Tarantool space '{SpaceName}' processing completed", spaceName);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Failed to create Tarantool space: {SpaceName}. Error: {Error}", spaceName, ex.Message);
				throw;
			}
		}

		private async Task EnsureConnectionAsync()
		{
			if (_box == null || _disposed || !_initialized)
			{
				try
				{
					_box?.Dispose();
					_initialized = false;
					await InitializeConnectionAsync();
					_initialized = true;
					_logger?.LogDebug("Tarantool connection re-established successfully");
				}
				catch (Exception ex)
				{
					_logger?.LogError(ex, "Failed to re-establish Tarantool connection to {Host}:{Port}", _host, _port);
					throw;
				}
			}
		}

		public async Task PublishMessageAsync(string topicName, string key, string message)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(TarantoolService));

			try
			{
				await EnsureInitializedAsync();

				// Используем InputChannel из конфигурации
				_logger?.LogDebug("Publishing message with key to Tarantool space: {SpaceName}, Key: {Key}", _inputSpace, key);

				await _semaphore.WaitAsync();
				try
				{
					await EnsureConnectionAsync();
				}
				finally
				{
					_semaphore.Release();
				}

				var now = DateTime.UtcNow.ToString("O");
				uint? id = null; // Auto-increment
				var messageWithKey = JsonSerializer.Serialize(new { key, message });
				var tuple = TarantoolTuple.Create(id, messageWithKey, now, _streamName);

				await _inputSpaceHandle.Insert(tuple);

				_logger?.LogInformation("Message with key published to Tarantool space: {SpaceName}, Key: {Key}, Timestamp: {Timestamp}",
					_inputSpaceHandle, key, now);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Error publishing message with key to Tarantool space: {SpaceName}", _inputSpace);
				throw;
			}
		}

		public async Task StartListeningAsync(string queueName, CancellationToken cancellationToken)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(TarantoolService));

			try
			{
				await EnsureInitializedAsync();

				// Используем OutputChannel из конфигурации для прослушивания ответов
				_logger?.LogInformation("Starting Tarantool listener for space: {SpaceName} (configured as OutputSpace)", _outputSpace);

				await _semaphore.WaitAsync();
				try
				{
					await EnsureConnectionAsync();
				}
				finally
				{
					_semaphore.Release();
				}

				_logger?.LogInformation("Tarantool listener started for space: {SpaceName}, Stream: {StreamName}",
					_outputSpace, _streamName);

				var lastProcessedId = 0u; // Отслеживаем последний обработанный ID

				// Tarantool polling (так как нет встроенного streaming)
				while (!cancellationToken.IsCancellationRequested)
				{
					try
					{
						// Запрос новых сообщений с ID больше последнего обработанного
						// Use the primary index with iterator for better performance
						var primaryIndex = _outputSpaceHandle[0u]; // Primary index (ID 0) - using indexer
						var result = await primaryIndex.Select<TarantoolTuple<uint>, TarantoolTuple<uint, string, string, string>>(
							TarantoolTuple.Create(lastProcessedId),
							new SelectOptions { Iterator = Iterator.Gt, Limit = 100 });

						foreach (var tuple in result.Data)
						{
							try
							{
								var id = tuple.Item1;
								var message = tuple.Item2;
								var timestamp = tuple.Item3;
								var stream = tuple.Item4;

								_logger?.LogInformation("Received message from Tarantool space {SpaceName}: {Message}, Id: {Id}, Timestamp: {Timestamp}",
									_outputSpace, message, id, timestamp);

								// Обновляем последний обработанный ID
								lastProcessedId = id;

								// Здесь можно добавить логику обработки и подтверждения
								// Например, удаление обработанного сообщения с явным указанием типов:
								await _outputSpaceHandle.Delete<TarantoolTuple<uint>, TarantoolTuple<uint, string, string, string>>(
									TarantoolTuple.Create(id));
							}
							catch (Exception ex)
							{
								_logger?.LogError(ex, "Error processing message from Tarantool space {SpaceName}", _outputSpace);
							}
						}

						// Пауза между опросами для прослушивания входящих сообщений
						await Task.Delay(1000, cancellationToken);
					}
					catch (OperationCanceledException)
					{
						break;
					}
					catch (Exception ex)
					{
						_logger?.LogError(ex, "Error polling Tarantool space {SpaceName}", _outputSpace);
						await Task.Delay(5000, cancellationToken); // Пауза при ошибке
					}
				}

				_logger?.LogInformation("Tarantool listener stopped for space: {SpaceName}", _outputSpace);
			}
			catch (OperationCanceledException)
			{
				_logger?.LogInformation("Tarantool listener cancelled for space: {SpaceName}", _outputSpace);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Error starting Tarantool listener for space: {SpaceName}", _outputSpace);
				throw;
			}
		}

		public async Task<bool> TestConnectionAsync()
		{
			_logger?.LogDebug("TestConnectionAsync called. Disposed: {IsDisposed}", _disposed);

			if (_disposed)
			{
				_logger?.LogWarning("Cannot test connection - TarantoolService is disposed");
				return false;
			}

			try
			{
				_logger?.LogDebug("Testing Tarantool connection to {Host}:{Port}...", _host, _port);

				// Пытаемся инициализировать соединение
				await EnsureInitializedAsync();

				await _semaphore.WaitAsync();
				try
				{
					await EnsureConnectionAsync();
					_logger?.LogDebug("Client connection ensured");
				}
				finally
				{
					_semaphore.Release();
				}

				// Простой тест - проверяем, что можем получить schema
#pragma warning disable CS0618 // Type or member is obsolete
				var schema = _box.GetSchema();
#pragma warning restore CS0618 // Type or member is obsolete

				// Проверяем, что наши spaces доступны
				var inputSpace = schema[_inputSpace];
				var outputSpace = schema[_outputSpace];

				_logger?.LogInformation("Tarantool connection test successful for {Host}:{Port}, Spaces: {InputSpace}, {OutputSpace}",
					_host, _port, _inputSpace, _outputSpace);
				return true;
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Tarantool connection test failed for {Host}:{Port}. Exception: {ExceptionType}, Message: {Message}",
					_host, _port, ex.GetType().Name, ex.Message);
				return false;
			}
		}

		public string GetBusType()
		{
			return "tarantool";
		}

		// Синхронный Dispose для совместимости с DI контейнером
		public void Dispose()
		{
			_logger?.LogDebug("Tarantool dispose() called");
			lock (_disposeLock)
			{
				if (_disposed) return;

				try
				{
					_logger?.LogDebug("Starting synchronous dispose of Tarantool client...");

					_box?.Dispose();
					_semaphore?.Dispose();
					_disposed = true;
					_initialized = false;
					_logger?.LogDebug("Tarantool client disposed (sync)");
				}
				catch (Exception ex)
				{
					_logger?.LogWarning(ex, "Error disposing Tarantool client (sync)");
					_disposed = true;
					_initialized = false;
				}
			}
		}

		// Асинхронный Dispose для оптимального освобождения ресурсов
		public ValueTask DisposeAsync()
		{
			_logger?.LogDebug("Tarantool disposeAsync() called");

			if (_disposed) return ValueTask.CompletedTask;

			lock (_disposeLock)
			{
				if (_disposed) return ValueTask.CompletedTask;
				_disposed = true;
				_initialized = false;
			}

			try
			{
				_logger?.LogDebug("Starting asynchronous dispose of Tarantool client...");

				_box?.Dispose();
				_semaphore?.Dispose();
				_logger?.LogDebug("Tarantool client disposed (async)");
			}
			catch (Exception ex)
			{
				_logger?.LogWarning(ex, "Error disposing Tarantool client (async)");
			}

			GC.SuppressFinalize(this);
			return ValueTask.CompletedTask;
		}
	}
}