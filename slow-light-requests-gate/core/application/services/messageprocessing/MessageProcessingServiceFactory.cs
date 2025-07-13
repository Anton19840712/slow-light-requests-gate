using lazy_light_requests_gate.core.application.interfaces.messageprocessing;

namespace lazy_light_requests_gate.core.application.services.messageprocessing
{
	public class MessageProcessingServiceFactory : IMessageProcessingServiceFactory
	{
		private readonly IServiceProvider _serviceProvider;
		private static string _currentDatabaseType;

		public MessageProcessingServiceFactory(IServiceProvider serviceProvider, IConfiguration configuration)
		{
			_serviceProvider = serviceProvider;
			if (string.IsNullOrEmpty(_currentDatabaseType))
			{
				_currentDatabaseType = configuration["Database"]?.ToString()?.ToLower() ?? "mongo";
			}
		}

		public MessageProcessingServiceBase CreateMessageProcessingService(string databaseType)
		{
			var dbType = databaseType?.ToLower() ?? _currentDatabaseType;

			return dbType switch
			{
				"postgres" => _serviceProvider.GetRequiredService<MessageProcessingPostgresService>(),
				"mongo" => _serviceProvider.GetRequiredService<MessageProcessingMongoService>(),
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
}
