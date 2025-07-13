namespace lazy_light_requests_gate.temp.models
{
    public class BusInformationDto
    {
        //public string Key { get; set; }
		public string InstanceId { get; set; }
		//public string ConnectionId { get; set; }
		public string TypeToRun { get; set; }
	}

	public class ActiveMqInformationDto : BusInformationDto
	{
		public string BrokerUri { get; set; }
		public string BrokerId { get; set; }
		public string BrokerUrl { get; set; }
	}
}
