namespace Derail;

public class SystemMessageFrame
{
    public SystemMessageFrame()
    {
        TimestampCreated = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
    }
    
    public long TimestampCreated { get; set; }
    public string SourceInstanceId { get; set; }
    public dynamic Payload { get; set; }
}