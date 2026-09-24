using System.Text.RegularExpressions;

namespace iptv.Services._Canonical;

public readonly record struct QualityInfo(string Normalized, int Rank, bool IsAuto);

/// <summary>
/// Parses arbitrary quality strings (e.g. "1080p", "576i", "1280p", "1080", "Auto", null) into a
/// normalized label and a numeric rank. "Auto"/master/unknown is flagged separately (IsAuto) so the
/// stream selector can treat adaptive master playlists as their own class instead of rank-0 worst.
/// </summary>
public static partial class StreamQualityParser
{
    [GeneratedRegex(@"(\d{3,4})\s*([pi])?", RegexOptions.IgnoreCase)]
    private static partial Regex ResolutionPattern();

    private static readonly HashSet<string> AutoTokens =
        new(StringComparer.OrdinalIgnoreCase) { "auto", "master", "hls", "http", "https", "direct", "" };

    public static QualityInfo Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new QualityInfo("Auto", 0, true);

        var trimmed = raw.Trim();

        if (AutoTokens.Contains(trimmed))
            return new QualityInfo("Auto", 0, true);

        var match = ResolutionPattern().Match(trimmed);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var height) && height is >= 100 and <= 4320)
        {
            var suffix = match.Groups[2].Success
                ? match.Groups[2].Value.ToLowerInvariant()
                : "p";

            return new QualityInfo($"{height}{suffix}", height, false);
        }

        return new QualityInfo("Auto", 0, true);
    }
}
