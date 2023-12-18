namespace Derail.EIP;

public class ClientServiceOptions: ServiceOptions
{
    public string Gateway { get; set; }
    public string Path { get; set; }
    public libplctag.PlcType PlcType { get; set; }
    public libplctag.Protocol Protocol { get; set; }
    public TimeSpan Timeout { get; set; }
    public TimeSpan ReadInterval { get; set; }
    public List<Tag> Tags { get; set; }
}

public class Tag
{
    public string Mapper { get; set; }
    public string Name { get; set; }
}