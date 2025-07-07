namespace lazy_light_requests_gate.core.application.interfaces.buses
{
	/// <summary>
	/// Интерфейс для динамического управления шинами сообщений
	/// </summary>
	public interface IDynamicBusManager
	{
		/// <summary>
		/// Переподключение к шине с новыми параметрами
		/// </summary>
		Task ReconnectWithNewParametersAsync(string busType, Dictionary<string, object> connectionParameters);

		/// <summary>
		/// Получение информации о текущем подключении
		/// </summary>
		Task<Dictionary<string, object>> GetCurrentConnectionInfoAsync();

		/// <summary>
		/// Восстановление конфигурации по умолчанию
		/// </summary>
		void RestoreDefaultConfiguration(string busType);
	}
}