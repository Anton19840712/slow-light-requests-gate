namespace lazy_light_requests_gate.messaging
{
	public interface IMessageProcessingServiceFactory
	{
		IMessageProcessingService CreateMessageProcessingService(string databaseType);
		void SetDefaultDatabaseType(string databaseType);
		string GetCurrentDatabaseType();
	}
}
