using System.Net;
using System.Net.Sockets;
using lazy_light_requests_gate.core.application.interfaces.messaging;
using lazy_light_requests_gate.core.application.interfaces.networking;

namespace lazy_light_requests_gate.infrastructure.networking
{
	public class TcpNetworkServer : INetworkServer
	{
		private readonly ILogger<TcpNetworkServer> _logger;
		private CancellationTokenSource _cts;
		private Task _serverTask;
		private TcpListener _listener;
		private readonly string _host;
		private readonly int _port;
		private readonly string _outQueue;
		private readonly string _protocol;
		private readonly IServiceScopeFactory _scopeFactory;

		public TcpNetworkServer(
			ILogger<TcpNetworkServer> logger,
			IServiceScopeFactory scopeFactory,
			IConfiguration configuration)
		{
			_logger = logger;
			_scopeFactory = scopeFactory;

			_host = configuration?["host"]?.ToString() ?? "localhost";  // Значение по умолчанию
			_port = int.TryParse(configuration?["port"]?.ToString(), out var p) ? p : 6254;  // Значение по умолчанию
			_outQueue = configuration["OutputChannel"] ?? "default-output-channel";
			_protocol = configuration["ProtocolTcpValue"];
		}

		public string Protocol => _protocol;
		public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			if (IsRunning) return;
			_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			_listener = new TcpListener(IPAddress.Parse(_host), _port);
			_listener.Start();

			_serverTask = Task.Run(async () =>
			{
				_logger.LogInformation("[TCP] Сервер запущен на {_host}:{_port}", _host, _port);

				// Периодическое логирование каждые 3 секунды
				var logInterval = TimeSpan.FromSeconds(3);
				var nextLogTime = DateTime.Now + logInterval;

				while (!_cts.Token.IsCancellationRequested)
				{
					try
					{
						// Логирование статуса сервера каждые 3 секунды
						if (DateTime.Now >= nextLogTime)
						{
							_logger.LogInformation("[TCP] Сервер работает на {_host}:{_port}", _host, _port);
							nextLogTime = DateTime.Now + logInterval;
						}

						if (_listener.Pending())
						{
							var client = await _listener.AcceptTcpClientAsync(_cts.Token);
							_logger.LogInformation("[TCP] Принято новое соединение от {0}", client.Client.RemoteEndPoint);

							// Здесь можно обработать клиента в отдельной задаче
							_ = HandleClientAsync(client, _cts.Token);
						}
						else
						{
							await Task.Delay(100, _cts.Token); // маленькая "пауза", чтобы не крутить цикл впустую
						}
					}
					catch (OperationCanceledException)
					{
						_logger.LogInformation("[TCP] Сервер отменён");
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "[TCP] Ошибка при обработке подключения");
					}
				}

				_listener.Stop();
				_logger.LogInformation("[TCP] Сервер остановлен");
			}, _cts.Token);
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
				_logger.LogInformation("[TCP] Задача остановки была отменена");
			}

			_logger.LogInformation("[TCP] Сервер остановлен");
		}

		private async Task HandleClientAsync(TcpClient client, CancellationToken token)
		{
			using var stream = client.GetStream();

			try
			{
				_logger.LogInformation("[TCP] Клиент подключён: {0}", client.Client.RemoteEndPoint);

				using var scope = _scopeFactory.CreateScope();
				var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

				var context = new TcpConnectionContext(client);

				await messageSender.SendMessagesToClientAsync(
					connectionContext: context,
					queueForListening: _outQueue,
					cancellationToken: token);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[TCP] Ошибка при отправке клиенту");
			}
			finally
			{
				client.Close();
				_logger.LogInformation("[TCP] Клиент отключён");
			}
		}
	}
}
