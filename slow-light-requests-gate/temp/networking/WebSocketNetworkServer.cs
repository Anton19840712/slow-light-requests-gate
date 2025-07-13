using System.Net;
using System.Net.WebSockets;
using System.Text;
using application.interfaces.networking;
using application.interfaces.services;

namespace infrastructure.networking
{
	public class WebSocketNetworkServer : INetworkServer
	{
		private readonly ILogger<WebSocketNetworkServer> _logger;
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly string _host;
		private readonly int _port;
		private readonly string _outQueue;
		private readonly string _protocol;
		private CancellationTokenSource _cts;
		private Task _serverTask;
		private WebSocketListener _listener;

		public WebSocketNetworkServer(
			ILogger<WebSocketNetworkServer> logger,
			IServiceScopeFactory scopeFactory,
			IConfiguration configuration)
		{
			_logger = logger;
			_scopeFactory = scopeFactory;

			_host = configuration["host"] ?? "localhost";
			_port = int.TryParse(configuration["port"], out var p) ? p : 5000;
			_outQueue = configuration["OutputChannel"] ?? "default-output-channel";
			_protocol = configuration["ProtocolTcpValue"];
		}

		public string Protocol => _protocol;
		public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

		public Task StartAsync(CancellationToken cancellationToken)
		{
			if (IsRunning) return Task.CompletedTask;

			_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			_listener = new WebSocketListener(_host, _port, _scopeFactory, _logger, _outQueue);
			_serverTask = _listener.StartAsync(_cts.Token);

			_logger.LogInformation("[WS] Сервер запущен на {Host}:{Port}", _host, _port);
			return Task.CompletedTask;
		}

		public async Task StopAsync(CancellationToken cancellationToken)
		{
			if (!IsRunning) return;

			_cts.Cancel();

			try
			{
				await _serverTask;
			}
			catch (TaskCanceledException)
			{
				_logger.LogInformation("[WS] Задача остановки была отменена");
			}
			_logger.LogInformation("[WS] Сервер остановлен");
		}
	}

	public class WebSocketListener
	{
		private readonly HttpListener _httpListener;
		private readonly string _url;
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly ILogger _logger;
		private readonly string _queueName;

		public WebSocketListener(string host, int port, IServiceScopeFactory scopeFactory, ILogger logger, string queueName)
		{
			_url = $"http://{host}:{port}/ws/";
			_httpListener = new HttpListener();
			_httpListener.Prefixes.Add(_url);
			_scopeFactory = scopeFactory;
			_logger = logger;
			_queueName = queueName;
		}

		public async Task StartAsync(CancellationToken token)
		{
			_httpListener.Start();
			_logger.LogInformation("[WS] HTTP Listener запущен на {Url}", _url);

			while (!token.IsCancellationRequested)
			{
				HttpListenerContext context;
				try
				{
					context = await _httpListener.GetContextAsync();
				}
				catch when (token.IsCancellationRequested)
				{
					break;
				}

				if (context.Request.IsWebSocketRequest)
				{
					_ = HandleConnectionAsync(context, token);
				}
				else
				{
					context.Response.StatusCode = 400;
					await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Only WebSocket supported"), token);
					context.Response.Close();
				}
			}

			_httpListener.Stop();
		}

		private async Task HandleConnectionAsync(HttpListenerContext context, CancellationToken token)
		{
			try
			{
				var wsContext = await context.AcceptWebSocketAsync(null);
				var socket = wsContext.WebSocket;

				_logger.LogInformation("[WS] Клиент подключился");

				using var scope = _scopeFactory.CreateScope();
				var sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

				var buffer = new byte[1024 * 4];
				while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
				{
					var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

					if (result.MessageType == WebSocketMessageType.Close)
					{
						await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", token);
						break;
					}

					var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

					_logger.LogInformation(""); // Пустая строка для визуального разделения
					_logger.LogInformation("[WS] Получено сообщение: {Message}", message);

					string reply = "Ответ от WebSocket-сервера: " + message;
					var replyBytes = Encoding.UTF8.GetBytes(reply);
					await socket.SendAsync(new ArraySegment<byte>(replyBytes), WebSocketMessageType.Text, true, token);
					_logger.LogInformation("[WS] Отправлено сообщение: {Reply}", reply);

					var contextObj = new WebSocketConnectionContext(socket);

					await sender.SendMessagesToClientAsync(
						connectionContext: contextObj,
						queueForListening: _queueName,
						cancellationToken: token);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[WS] Ошибка при обработке подключения");
			}
		}
	}
}
