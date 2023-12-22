namespace Derail.MQTT;

public class ClientServiceOptions: ServiceOptions
{
    public string BrokerAddress { get; set; }
    public int BrokerPort { get; set; }
    public string ClientId { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public bool UseTls { get; set; }
    public TimeSpan ReconnectInterval { get; set; }
    public List<SubscriptionTopic> SubscriptionTopics { get; set; }
}

public class SubscriptionTopic
{
    public bool Enabled { get; set; }
    public string Topic { get; set; }
}