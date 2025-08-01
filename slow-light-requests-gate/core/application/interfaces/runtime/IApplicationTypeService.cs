﻿using lazy_light_requests_gate.core.domain.events;
using lazy_light_requests_gate.presentation.enums;

namespace lazy_light_requests_gate.core.application.interfaces.runtime
{
	public interface IApplicationTypeService
	{
		ApplicationType GetApplicationType();
		Task SetApplicationTypeAsync(ApplicationType type);
		bool IsRestEnabled();
		bool IsStreamEnabled();
		bool IsBothEnabled();
		string GetDescription();

		// События для уведомления об изменениях
		event EventHandler<ApplicationTypeChangedEventArgs> ApplicationTypeChanged;
	}
}
