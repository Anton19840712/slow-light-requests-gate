using lazy_light_requests_gate.temp;
using RabbitMQ.Client;

public interface IRabbitMqService : IMessageBusService
{
	IConnection CreateConnection();	
}
