namespace iptv.Services._Stream.DTOs.Results;

public class StreamFilteredResult
{
    public string StreamId { get; set; }
    public string ChannelId { get; set; }
    public string Name { get; set; }
    public string StreamUri { get; set; }
    public string Type { get; set; }
    public string? Quality { get; set; }
    public bool IsHealthy { get; set; }
    public bool Inactive { get; set; }
    public DateTime? LastCheckedMoment { get; set; }
}
