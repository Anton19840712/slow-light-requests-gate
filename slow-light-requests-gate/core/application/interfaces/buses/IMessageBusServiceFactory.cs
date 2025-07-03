namespace lazy_light_requests_gate.core.application.interfaces.buses
{
	/// <summary>
	/// Интерфейс фабрики для создания сервисов шин сообщений
	/// </summary>
	public interface IMessageBusServiceFactory
	{
		IMessageBusService CreateMessageBusService(string busType);
		void SetDefaultBusType(string busType);
		string GetCurrentBusType();
		Task<bool> TestCurrentBusConnectionAsync();
	}
}
