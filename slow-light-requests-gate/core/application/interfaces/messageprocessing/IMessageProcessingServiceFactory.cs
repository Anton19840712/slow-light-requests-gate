using lazy_light_requests_gate.infrastructure.services.messageprocessing;

namespace lazy_light_requests_gate.core.application.interfaces.messageprocessing
{
	public interface IMessageProcessingServiceFactory
	{
		MessageProcessingServiceBase CreateMessageProcessingService(string databaseType);
		void SetDefaultDatabaseType(string databaseType);
		string GetCurrentDatabaseType();
	}
}
