using System.Net.Sockets;
using System.Text;
using application.interfaces.networking;
using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.core.application.services.messageprocessing;

namespace infrastructure.networking
{
	public class UdpNetworkClient : INetworkClient
	{
		private readonly ILogger<UdpNetworkClient> _logger;
		private readonly IMessageProcessingServiceFactory _messageProcessingServiceFactory;
		private readonly string _host;
		private readonly int _port;
		private readonly string _outQueue;
		private readonly string _inQueue;
		private readonly string _protocol;
		private CancellationTokenSource _cts;
		private Task _clientTask;
		private int _attempt = 0;

		private const int MaxDelayMilliseconds = 60000;

		public UdpNetworkClient(
			ILogger<UdpNetworkClient> logger,
			IMessageProcessingServiceFactory messageProcessingServiceFactory,
			IConfiguration configuration)
		{
			_logger = logger;
			_messageProcessingServiceFactory = messageProcessingServiceFactory;

			_host = configuration["host"] ?? "localhost";
			_port = int.TryParse(configuration["port"], out var p) ? p : 5019;

			_inQueue = configuration["InputChannel"] ?? "default-input-channel";
			_outQueue = configuration["OutputChannel"] ?? "default-output-channel";
			_protocol = configuration["ProtocolUdpValue"];
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
			var buffer = new byte[4096];

			while (!token.IsCancellationRequested)
			{
				using var udpClient = new UdpClient();
				try
				{
					udpClient.Connect(_host, _port);

					string helloMessage = "Привет от клиента!";
					byte[] helloBytes = Encoding.UTF8.GetBytes(helloMessage);
					await udpClient.SendAsync(helloBytes, helloBytes.Length);
					_logger.LogInformation("[UDP Client] Отправлено приветственное сообщение: {Message}", helloMessage);

					while (!token.IsCancellationRequested)
					{
						var result = await udpClient.ReceiveAsync(token);

						var message = Encoding.UTF8.GetString(result.Buffer);
						_logger.LogInformation($"[UDP Client] {message} получено успешно.");

						string replyMessage = $"Ответ на: {message}";
						byte[] replyBytes = Encoding.UTF8.GetBytes(replyMessage);
						await udpClient.SendAsync(replyBytes, replyBytes.Length);
						_logger.LogInformation("[UDP Client] Отправлен ответ серверу: {Message}", replyMessage);

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
					_logger.LogInformation("[UDP Client] Остановка по токену отмены");
					break;
				}
				catch (SocketException ex)
				{
					_attempt++;
					int delay = Math.Min(1000 * (int)Math.Pow(2, _attempt), MaxDelayMilliseconds);
					_logger.LogWarning("[UDP Client] Попытка {_attempt}: ошибка — {Message}. Повтор через {Delay} мс",
						_attempt, ex.Message, delay);
					await SafeDelayAsync(delay, token);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[UDP Client] Критическая ошибка. Клиент остановлен.");
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
				// Игнорируется
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
				_logger.LogInformation("[UDP Client] Клиент остановлен");
			}
		}
	}
}
