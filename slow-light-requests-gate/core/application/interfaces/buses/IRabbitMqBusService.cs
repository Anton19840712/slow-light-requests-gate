using lazy_light_requests_gate.core.application.interfaces.buses;
using RabbitMQ.Client;

public interface IRabbitMqBusService : IMessageBusService
{
	Task<string> WaitForResponseAsync(string queueName, int timeoutMilliseconds = 15000);
	IConnection CreateConnection();
}

