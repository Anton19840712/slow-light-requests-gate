﻿namespace domain.models.response
{
	/// <summary>
	/// Частная модель для работы с возвратом информации из сервиса по созданию очередей.
	/// </summary>
	public class ResponseQueuesIntegration : ResponseIntegration
	{
		public string OutQueue { get; set; }
	}
}
