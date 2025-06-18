namespace lazy_light_requests_gate.temp
{
	public static class MessageBusConfiguration
	{
		public static IServiceCollection AddMessageBusServices(this IServiceCollection services)
		{
			services.AddSingleton<IMessageBusConfigurationProvider, MessageBusConfigurationProvider>();
			services.AddSingleton<IMessageBusFactory, MessageBusFactory>();
			services.AddSingleton<IUnifiedMessageBusManager, UnifiedMessageBusManager>();

			return services;
		}
	}
}
