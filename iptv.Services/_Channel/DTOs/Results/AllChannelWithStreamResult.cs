namespace iptv.Services._Channel.DTOs.Results;

public class AllChannelWithStreamResult
{
    public string ChannelId { get; set; }
    public string Name { get; set; }
    public string ImageUri { get; set; }
    public string Country { get; set; }
    public string Category { get; set; }

    public string CurrentStreamUrl { get; set; }

    public string StreamUserAgent { get; set; }
    public string StreamReferer { get; set; }
    public string StreamQuality { get; set; }
}