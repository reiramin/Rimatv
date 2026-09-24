using iptv.Domain.Collections;

namespace iptv.Services._Channel.Playability;

public sealed class StatusStreamInput
{
    public string Type { get; set; }
    public string RequiredRegion { get; set; }
}

public sealed class ChannelStatusInput
{
    public string CuratedCountry { get; set; }
    public string SourceCountry { get; set; }
    public string ChannelCountry { get; set; }
    // The channel's eligible streams (never empty: a channel without one is not returned).
    public List<StatusStreamInput> Streams { get; set; } = [];
    public string VpnHelpUrl { get; set; }
}

public sealed class ChannelStatusResult
{
    public string Status { get; set; }
    public List<string> RequiredRegions { get; set; } = [];
    public string ErrorCode { get; set; }
    public string Message { get; set; }
    public string MessageFa { get; set; }
    public string VpnHelpUrl { get; set; }
    public string Country { get; set; }
}

/// <summary>
/// Channel-level playability status (§2.2). The SAME rules are implemented in Python in
/// RimaTv_Data/scripts/channel_rules.py; a parity test feeds both the same fixtures — change both.
///
///   A stream is "open" when it needs no VPN: RequiredRegion is null and it is not YouTube
///   (YouTube is filtered in Iran), or the channel is an "iran" channel and RequiredRegion is IR.
///   status          = "ok" if any eligible stream is open, else "regionRestricted".
///   requiredRegions = [] when ok, else the distinct RequiredRegions of the eligible streams
///                     (ordinal-sorted). YouTube-only channels get [].
///   errorCode       = "USE_VPN" when regionRestricted, else null (omitted).
///   message(Fa)     = "only available in {regions}" (comma-joined codes) / generic VPN message when
///                     no region is known; null when ok.
///   vpnHelpUrl      = config Client:VpnHelpUrl when regionRestricted and configured, else null.
///   country         = ISO grouping code (see <see cref="ResolveDisplayCountry"/>).
/// </summary>
public static class ChannelStatusRules
{
    public const string StatusOk = "ok";
    public const string StatusRegionRestricted = "regionRestricted";
    public const string UseVpn = "USE_VPN";

    public static ChannelStatusResult Compute(ChannelStatusInput input)
    {
        var iran = string.Equals(input.CuratedCountry, "iran", StringComparison.OrdinalIgnoreCase);
        var streams = input.Streams ?? [];

        var result = new ChannelStatusResult
        {
            Country = ResolveDisplayCountry(input.CuratedCountry, input.SourceCountry, input.ChannelCountry)
        };

        if (streams.Any(s => IsOpen(s, iran)))
        {
            result.Status = StatusOk;
            return result;
        }

        result.Status = StatusRegionRestricted;
        result.ErrorCode = UseVpn;
        result.RequiredRegions = DistinctRegions(streams.Select(s => s.RequiredRegion));
        (result.Message, result.MessageFa) = VpnMessages(result.RequiredRegions);
        result.VpnHelpUrl = string.IsNullOrWhiteSpace(input.VpnHelpUrl) ? null : input.VpnHelpUrl.Trim();
        return result;
    }

    public static (string En, string Fa) VpnMessages(IReadOnlyCollection<string> regions)
    {
        if (regions == null || regions.Count == 0)
            return ("A VPN is needed to watch this channel.",
                    "برای تماشای این شبکه به VPN نیاز دارید.");

        var joined = string.Join(", ", regions);
        return ($"This channel is only available in {joined}. Please use a VPN.",
                $"این شبکه فقط در {joined} قابل پخش است؛ لطفاً از VPN استفاده کنید.");
    }

    public static List<string> DistinctRegions(IEnumerable<string> regions)
        => regions
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

    private static bool IsOpen(StatusStreamInput s, bool iranChannel)
    {
        var region = string.IsNullOrWhiteSpace(s.RequiredRegion) ? null : s.RequiredRegion.Trim().ToUpperInvariant();
        if (iranChannel && region == "IR")
            return true;
        return region == null && s.Type != StreamTypes.YouTube;
    }

    /// <summary>
    /// The Flutter app groups channels by the ISO <c>country</c> field, so it must never be empty
    /// (M3U sources such as shayanline carry no tvg-country). Persian channels (iran / iran-foreign)
    /// are reported as IR so they are grouped together for Iranian users; other two-letter curated
    /// keys map to their ISO code; otherwise SourceCountry, then the channel's own country;
    /// iptv-org's non-standard "UK" becomes "GB".
    /// </summary>
    public static string ResolveDisplayCountry(string curatedCountry, string sourceCountry, string channelCountry)
    {
        if (string.Equals(curatedCountry, "iran", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(curatedCountry, "iran-foreign", StringComparison.OrdinalIgnoreCase))
            return "IR";

        if (curatedCountry?.Length == 2)
            return NormalizeIso(curatedCountry);

        if (!string.IsNullOrWhiteSpace(sourceCountry))
            return NormalizeIso(sourceCountry);

        return NormalizeIso(channelCountry);
    }

    private static string NormalizeIso(string country)
    {
        var c = (country ?? string.Empty).Trim().ToUpperInvariant();
        return c == "UK" ? "GB" : c;
    }
}
