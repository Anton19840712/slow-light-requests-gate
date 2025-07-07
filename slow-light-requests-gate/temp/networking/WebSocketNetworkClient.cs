using System.Net.WebSockets;
using System.Text;
using application.interfaces.networking;
using application.interfaces.services;

namespace infrastructure.networking
{
	public class WebSocketNetworkClient : INetworkClient
	{
		private readonly ILogger<WebSocketNetworkClient> _logger;
		private readonly IMessageProcessingService _messageProcessingService;
		private readonly string _host;
		private readonly int _port;
		private readonly string _outQueue;
		private readonly string _inQueue;
		private CancellationTokenSource _cts;
		private Task _clientTask;

		private const int MaxDelayMilliseconds = 60000;

		public WebSocketNetworkClient(
			ILogger<WebSocketNetworkClient> logger,
			IMessageProcessingService messageProcessingService,
			IConfiguration configuration)
		{
			_logger = logger;
			_messageProcessingService = messageProcessingService;

			_host = configuration["host"] ?? "localhost";
			_port = int.TryParse(configuration["port"], out var p) ? p : 5018;

			var companyName = configuration["CompanyName"] ?? "default";
			_outQueue = companyName + "_out";
			_inQueue = companyName + "_in";
		}

		public string Protocol => "ws";// должен быть match с передаваемым через json параметром.
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
				using var client = new ClientWebSocket();

				try
				{
					var uri = new Uri($"ws://{_host}:{_port}/");
					await client.ConnectAsync(uri, token);
					_logger.LogInformation("[WS Client] Подключено к серверу {Uri}", uri);

					string helloMessage = $"Привет от ws клиента по адресу: {uri}!";
					var helloBytes = Encoding.UTF8.GetBytes(helloMessage);
					await client.SendAsync(new ArraySegment<byte>(helloBytes), WebSocketMessageType.Text, true, token);
					_logger.LogInformation("[WS Client] Отправлено сообщение: {Message}", helloMessage);

					var buffer = new byte[1024];
					var messageBuffer = new ArraySegment<byte>(new byte[8192]); // можно увеличить при необходимости

					while (!token.IsCancellationRequested && client.State == WebSocketState.Open)
					{
						var messageSegments = new List<byte>();

						WebSocketReceiveResult result;

						do
						{
							result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), token);
							messageSegments.AddRange(buffer.Take(result.Count));

							if (result.MessageType == WebSocketMessageType.Close)
							{
								_logger.LogWarning("[WS Client] Сервер закрыл соединение");
								await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Закрытие от клиента", token);
								return;
							}

						} while (!result.EndOfMessage);

						var fullMessage = Encoding.UTF8.GetString(messageSegments.ToArray());

						_logger.LogInformation(""); // Пустая строка для визуального разделения
						_logger.LogInformation("[WS Client] Получено сообщение: {Message}", fullMessage);

						if (fullMessage == "Ping")
						{
							string pong = "Pong";
							var pongBytes = Encoding.UTF8.GetBytes(pong);
							await client.SendAsync(new ArraySegment<byte>(pongBytes), WebSocketMessageType.Text, true, token);
							_logger.LogInformation("[WS Client] Отправлено сообщение: {Message}", pong);
						}

						await _messageProcessingService.ProcessIncomingMessageAsync(
							message: fullMessage,
							instanceModelQueueOutName: _outQueue,
							instanceModelQueueInName: _inQueue,
							host: _host,
							port: _port,
							protocol: "ws");
					}
				}
				catch (OperationCanceledException)
				{
					_logger.LogInformation("[WS Client] Остановка по токену отмены");
					break;
				}
				catch (WebSocketException ex)
				{
					attempt++;
					int delay = Math.Min(1000 * (int)Math.Pow(2, attempt), MaxDelayMilliseconds);
					_logger.LogWarning("[WS Client] Попытка {Attempt}: ошибка соединения — {Message}. Повтор через {Delay} мс",
						attempt, ex.Message, delay);
					await SafeDelayAsync(delay, token);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[WS Client] Критическая ошибка. Клиент остановлен.");
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
				// ожидаемая отмена
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
				_logger.LogInformation("[WS Client] Клиент остановлен");
			}
		}
	}
}
