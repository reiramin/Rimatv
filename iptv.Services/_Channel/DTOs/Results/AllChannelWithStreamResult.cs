namespace iptv.Services._Channel.DTOs.Results;

public class FallbackStreamResult
{
    public string StreamId { get; set; }
    public string Url { get; set; }
    public string UserAgent { get; set; }
    public string Referer { get; set; }
    public string Quality { get; set; }
    public string ProviderName { get; set; }
}

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

    // Additive fields (§5.1).
    public string StreamId { get; set; }
    public string CanonicalId { get; set; }
    public string NameFa { get; set; }
    public string CuratedCountry { get; set; }
    public string ProviderName { get; set; }

    public List<FallbackStreamResult> FallbackStreams { get; set; } = [];
}
