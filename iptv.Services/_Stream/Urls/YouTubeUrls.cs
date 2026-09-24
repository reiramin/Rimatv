using System.Text.RegularExpressions;

namespace iptv.Services._Stream.Urls;

/// <summary>
/// Normalizes official YouTube live sources to embed URLs (the only form we store/emit):
/// <c>https://www.youtube.com/embed/&lt;videoId&gt;</c> or
/// <c>https://www.youtube.com/embed/live_stream?channel=&lt;UC…&gt;</c>.
/// Raw googlevideo URLs are never produced.
/// </summary>
public static partial class YouTubeUrls
{
    public const string EmbedBase = "https://www.youtube.com/embed/";

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex VideoIdPattern();

    [GeneratedRegex("^UC[A-Za-z0-9_-]{22}$")]
    private static partial Regex ChannelIdPattern();

    public static bool IsYouTubeHost(string url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        return host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "youtu.be"
               or "www.youtube-nocookie.com" or "youtube-nocookie.com";
    }

    public static string VideoEmbed(string videoId)
        => VideoIdPattern().IsMatch(videoId ?? "") ? EmbedBase + videoId : null;

    public static string ChannelLiveEmbed(string channelId)
        => ChannelIdPattern().IsMatch(channelId ?? "") ? $"{EmbedBase}live_stream?channel={channelId}" : null;

    /// <summary>Embed URL for a YouTube video/live URL, or null when it cannot be expressed as one
    /// (e.g. an @handle page, which would need a lookup).</summary>
    public static string ToEmbedUrl(string url)
    {
        if (!IsYouTubeHost(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return null;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var query = ParseQuery(uri.Query);

        if (uri.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase))
            return segments.Length > 0 ? VideoEmbed(segments[0]) : null;

        if (segments.Length == 0)
            return null;

        switch (segments[0].ToLowerInvariant())
        {
            case "watch":
                return VideoEmbed(query.GetValueOrDefault("v"));
            case "live":
                return segments.Length > 1 ? VideoEmbed(segments[1]) : null;
            case "embed":
                if (segments.Length > 1 && segments[1] == "live_stream")
                    return ChannelLiveEmbed(query.GetValueOrDefault("channel"));
                return segments.Length > 1 ? VideoEmbed(segments[1]) : null;
            case "channel":
                return segments.Length > 1 ? ChannelLiveEmbed(segments[1]) : null;
            default:
                return null;
        }
    }

    public static string VideoIdOf(string embedUrl)
    {
        if (string.IsNullOrEmpty(embedUrl) || !embedUrl.StartsWith(EmbedBase, StringComparison.Ordinal))
            return null;
        var rest = embedUrl[EmbedBase.Length..];
        return VideoIdPattern().IsMatch(rest) ? rest : null;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq > 0)
                result[part[..eq]] = Uri.UnescapeDataString(part[(eq + 1)..]);
        }
        return result;
    }
}
