namespace domain.models.dynamicgatesettings.internalusage
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
