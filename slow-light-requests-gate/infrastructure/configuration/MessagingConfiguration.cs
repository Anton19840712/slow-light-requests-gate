﻿using lazy_light_requests_gate.core.application.interfaces.messageprocessing;
using lazy_light_requests_gate.core.application.interfaces.messaging;
using lazy_light_requests_gate.infrastructure.services.messageprocessing;
using lazy_light_requests_gate.infrastructure.services.messaging;

namespace lazy_light_requests_gate.infrastructure.configuration
{
	static class MessagingConfiguration
	{
		/// <summary>
		/// Регистрация сервисов, участвующих в отсылке и получении сообщений на основе параметра Database.
		/// </summary>
		public static IServiceCollection AddMessageServingServices(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddScoped<IMessageProcessingServiceFactory, MessageProcessingServiceFactory>();
			services.AddScoped<MessageProcessingPostgresService>();
			services.AddScoped<MessageProcessingMongoService>();

			services.AddScoped<ConnectionMessageSenderFactory>();
			services.AddScoped<IMessageSender, MessageSender>();

			return services;
		}
	}
}
