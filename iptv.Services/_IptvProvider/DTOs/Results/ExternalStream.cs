namespace iptv.Services._IptvProvider.DTOs.Results;

public class ExternalStream
{
    public string Channel { get; set; }
    public string Url { get; set; }
    public string UserAgent { get; set; }
    public string Referrer { get; set; }
    public string? Quality { get; set; }
}
