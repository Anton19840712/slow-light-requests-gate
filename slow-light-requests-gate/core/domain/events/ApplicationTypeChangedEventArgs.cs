using lazy_light_requests_gate.presentation.enums;

namespace lazy_light_requests_gate.core.domain.events
{
	public class ApplicationTypeChangedEventArgs : EventArgs
	{
		public ApplicationType OldType { get; }
		public ApplicationType NewType { get; }
		public DateTime Timestamp { get; }

		public ApplicationTypeChangedEventArgs(ApplicationType oldType, ApplicationType newType)
		{
			OldType = oldType;
			NewType = newType;
			Timestamp = DateTime.UtcNow;
		}
	}
}
