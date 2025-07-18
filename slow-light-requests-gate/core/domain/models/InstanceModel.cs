﻿namespace lazy_light_requests_gate.core.domain.models
{
	/// <summary>
	/// Базовая модель для подключения
	/// </summary>
	public class InstanceModel
	{
		public string Protocol { get; set; }
		public string DataFormat { get; set; }
		public string InternalModel { get; set; }
		public string InQueueName { get; set; }
		public string OutQueueName { get; set; }
	}
}
