using application.interfaces.networking;
using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.core.application.services.messageprocessing;
using System.Net.Sockets;
using System.Text;

public class TcpNetworkClient : INetworkClient
{
	private readonly ILogger<TcpNetworkClient> _logger;
	private readonly IMessageProcessingServiceFactory _messageProcessingServiceFactory;
	private readonly string _host;
	private readonly int _port;
	private readonly string _outQueue;
	private readonly string _inQueue;
	private readonly string _protocol;
	private CancellationTokenSource _cts;
	private Task _clientTask;

	private const int MaxDelayMilliseconds = 60000; // максимум 1 минута между попытками

	public TcpNetworkClient(
		ILogger<TcpNetworkClient> logger,
		IMessageProcessingServiceFactory messageProcessingServiceFactory,
		IConfiguration configuration)
	{
		_logger = logger;
		_messageProcessingServiceFactory = messageProcessingServiceFactory;

		_host = configuration["host"] ?? "localhost";
		_port = int.TryParse(configuration["port"], out var p) ? p : 5019;

		_inQueue = configuration["InputChannel"] ?? "default-input-channel";
		_outQueue = configuration["OutputChannel"] ?? "default-output-channel";
		_protocol = configuration["ProtocolTcpValue"];
	}

	public string Protocol => _protocol;
	public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (IsRunning) return Task.CompletedTask;
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		_clientTask = Task.Run(() => RunClientLoopAsync(_cts.Token), _cts.Token);
		return Task.CompletedTask;
	}

	private async Task RunClientLoopAsync(CancellationToken token)
	{
		int attempt = 0;

		while (!token.IsCancellationRequested)
		{
			try
			{
				using var client = new TcpClient();
				await client.ConnectAsync(_host, _port);
				_logger.LogInformation("[TCP Client] Подключено к серверу {Host}:{Port}", _host, _port);

				using var stream = client.GetStream();
				var buffer = new byte[1024];
				attempt = 0; // сброс при успешном подключении

				while (!token.IsCancellationRequested)
				{
					// Чтение длины (4 байта)
					byte[] lengthPrefix = new byte[4];
					int read = await stream.ReadAsync(lengthPrefix, 0, 4, token);
					if (read == 0) break; // соединение закрыто

					if (!BitConverter.IsLittleEndian)
						Array.Reverse(lengthPrefix);
					int messageLength = BitConverter.ToInt32(lengthPrefix, 0);

					// Чтение самого сообщения
					byte[] payload = new byte[messageLength];
					int totalRead = 0;
					while (totalRead < messageLength)
					{
						int chunkRead = await stream.ReadAsync(payload, totalRead, messageLength - totalRead, token);
						if (chunkRead == 0) break;
						totalRead += chunkRead;
					}

					string message = Encoding.UTF8.GetString(payload, 0, totalRead);

					_logger.LogInformation(""); // Пустая строка для визуального разделения
					_logger.LogInformation("[TCP Client] Получено сообщение: {Message}", message);

					var messageProcessingService = _messageProcessingServiceFactory
						.CreateMessageProcessingService(_messageProcessingServiceFactory.GetCurrentDatabaseType());

					await messageProcessingService.ProcessForSaveIncomingMessageAsync(
						message: message,
						channelOut: _outQueue,
						channelIn: _inQueue,
						host: _host,
						port: _port,
						protocol: _protocol);
				}

			}
			catch (OperationCanceledException)
			{
				_logger.LogInformation("[TCP Client] Остановка по токену отмены");
				break;
			}
			catch (SocketException ex)
			{
				attempt++;
				int delay = Math.Min(1000 * (int)Math.Pow(2, attempt), MaxDelayMilliseconds);
				_logger.LogWarning("[TCP Client] Попытка {Attempt}: не удалось подключиться к {Host}:{Port} — {Message}. Повтор через {Delay} мс",
					attempt, _host, _port, ex.Message, delay);
				await SafeDelayAsync(delay, token);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[TCP Client] Критическая ошибка. Клиент остановлен.");
				break;
			}
		}
	}

	private async Task SafeDelayAsync(int delayMs, CancellationToken token)
	{
		try
		{
			await Task.Delay(delayMs, token);
		}
		catch (TaskCanceledException)
		{
			// Игнорируем — это ожидаемо при отмене
		}
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (!IsRunning) return;

		_cts.Cancel();

		try
		{
			await _clientTask;
		}
		catch (TaskCanceledException)
		{
			_logger.LogInformation("[TCP Client] Клиент остановлен");
		}
	}
}
