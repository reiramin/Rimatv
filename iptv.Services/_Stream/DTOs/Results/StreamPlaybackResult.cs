namespace iptv.Services._Stream.DTOs.Results;

public class PlaybackFallback
{
    public string StreamId { get; set; }
    public string StreamUri { get; set; }
    public string UserAgent { get; set; }
    public string Referer { get; set; }
    public string Quality { get; set; }
    public string ProviderName { get; set; }
}

public class StreamPlaybackResult
{
    public string ChannelId { get; set; }
    public string CanonicalId { get; set; }
    public string StreamId { get; set; }
    public string StreamUri { get; set; }
    public string UserAgent { get; set; }
    public string Referer { get; set; }
    public string Type { get; set; }
    public string Quality { get; set; }
    public string ProviderName { get; set; }

    public List<PlaybackFallback> Fallbacks { get; set; } = [];
}
