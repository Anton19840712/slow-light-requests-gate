using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.infrastructure.services.messageprocessing;

public class MessageProcessingServiceFactory : IMessageProcessingServiceFactory
{
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private static string _currentDatabaseType;

	public MessageProcessingServiceFactory(IServiceScopeFactory serviceScopeFactory, IConfiguration configuration)
	{
		_serviceScopeFactory = serviceScopeFactory;
		if (string.IsNullOrEmpty(_currentDatabaseType))
		{
			_currentDatabaseType = configuration["Database"]?.ToString()?.ToLower() ?? "mongo";
		}
	}

	public MessageProcessingServiceBase CreateMessageProcessingService(string databaseType)
	{
		var dbType = databaseType?.ToLower() ?? _currentDatabaseType;

		using var scope = _serviceScopeFactory.CreateScope();
		var serviceProvider = scope.ServiceProvider;

		return dbType switch
		{
			"postgres" => serviceProvider.GetRequiredService<MessageProcessingPostgresService>(),
			"mongo" => serviceProvider.GetRequiredService<MessageProcessingMongoService>(),
			_ => throw new ArgumentException($"Unsupported database type: {databaseType}")
		};
	}

	public void SetDefaultDatabaseType(string databaseType)
	{
		_currentDatabaseType = databaseType?.ToLower() ?? "mongo";
	}

	public string GetCurrentDatabaseType()
	{
		return _currentDatabaseType;
	}
}
