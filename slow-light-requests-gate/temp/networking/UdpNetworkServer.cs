using System.Net;
using System.Net.Sockets;
using System.Text;
using application.interfaces.networking;
using application.interfaces.services;

namespace infrastructure.networking
{
	public class UdpNetworkServer : INetworkServer
	{
		private readonly ILogger<UdpNetworkServer> _logger;
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly string _host;
		private readonly int _port;
		private readonly string _outQueue;
		private readonly string _protocol;
		private CancellationTokenSource _cts;
		private Task _serverTask;
		private UdpClient _udpClient;

		public UdpNetworkServer(
			ILogger<UdpNetworkServer> logger,
			IServiceScopeFactory scopeFactory,
			IConfiguration configuration)
		{
			_logger = logger;
			_scopeFactory = scopeFactory;

			_host = configuration["host"] ?? "0.0.0.0";
			_port = int.TryParse(configuration["port"], out var p) ? p : 5005;
			_outQueue = configuration["OutputChannel"] ?? "default-output-channel";
			_protocol = configuration["ProtocolTcpValue"];
		}

		public string Protocol => _protocol;
		public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			if (IsRunning) return;
			_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			_udpClient = new UdpClient(new IPEndPoint(IPAddress.Parse(_host), _port));
			_logger.LogInformation("[UDP] Сервер запущен на {Host}:{Port}", _host, _port);

			_serverTask = Task.Run(async () =>
			{
				var logInterval = TimeSpan.FromSeconds(3);
				var nextLogTime = DateTime.Now + logInterval;

				while (!_cts.Token.IsCancellationRequested)
				{
					try
					{
						if (DateTime.Now >= nextLogTime)
						{
							_logger.LogInformation("[UDP] Сервер работает на {Host}:{Port}", _host, _port);
							nextLogTime = DateTime.Now + logInterval;
						}

						var result = await _udpClient.ReceiveAsync(_cts.Token);
						_ = HandleIncomingMessage(result.Buffer, result.RemoteEndPoint, _cts.Token);
					}
					catch (OperationCanceledException)
					{
						_logger.LogInformation("[UDP] Сервер остановлен по токену отмены");
						break;
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "[UDP] Ошибка при получении данных");
					}
				}

				_udpClient.Close();
				_logger.LogInformation("[UDP] Сервер остановлен");
			}, _cts.Token);

			await Task.CompletedTask;
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
				_logger.LogInformation("[UDP] Задача остановки была отменена");
			}
		}

		private async Task HandleIncomingMessage(byte[] data, IPEndPoint remoteEndPoint, CancellationToken token)
		{
			var message = Encoding.UTF8.GetString(data);
			_logger.LogInformation("\n[UDP] Получено сообщение от {RemoteEndPoint}: {Message}", remoteEndPoint, message);

			try
			{
				using var scope = _scopeFactory.CreateScope();
				var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

				string replyMessage = "Ответ от udp сервера: " + message;
				byte[] replyBytes = Encoding.UTF8.GetBytes(replyMessage);
				await _udpClient.SendAsync(replyBytes, replyBytes.Length, remoteEndPoint);
				_logger.LogInformation("[UDP] Отправлено сообщение обратно клиенту: {Message}", replyMessage);

				var context = new UdpConnectionContext(_udpClient, remoteEndPoint);

				await messageSender.SendMessagesToClientAsync(
					connectionContext: context,
					queueForListening: _outQueue,
					cancellationToken: token);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[UDP] Ошибка при обработке сообщения");
			}
		}
	}
}
