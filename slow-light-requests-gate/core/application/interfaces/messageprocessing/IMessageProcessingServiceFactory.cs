namespace lazy_light_requests_gate.core.application.interfaces.messageprocessing
{
	public interface IMessageProcessingServiceFactory
	{
		IMessageProcessingService CreateMessageProcessingService(string databaseType);
		void SetDefaultDatabaseType(string databaseType);
		string GetCurrentDatabaseType();
	}
}
