using System.Security.Cryptography;
using System.Text;
using iptv.Domain.Collections;

namespace iptv.Services._Stream.Urls;

/// <summary>
/// Hook for a future relay hosted on a SEPARATE server (never on Render; no relay is implemented
/// here). Format: <c>{BaseUrl}/hls?u={base64url(streamUri)}&amp;exp={unix+6h}&amp;sig={sig}</c> where
/// <c>sig = base64url(HMACSHA256(SigningKey, "{u}|{exp}"))</c>.
/// </summary>
public static class RelayUrl
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);

    /// <summary>
    /// Relay URL, or null when the relay is not configured or the stream must not be relayed:
    /// broadcaster geo-locks (any RequiredRegion), YouTube, non-HLS and Iranian (IR) streams.
    /// </summary>
    public static string Build(
        string baseUrl, string signingKey, string streamUri, string type, string requiredRegion,
        bool relayEligible, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(signingKey))
            return null;

        if (!relayEligible || requiredRegion != null || type != StreamTypes.Hls ||
            string.IsNullOrWhiteSpace(streamUri))
            return null;

        var u = Base64Url(Encoding.UTF8.GetBytes(streamUri));
        var exp = new DateTimeOffset(utcNow.ToUniversalTime()).Add(Lifetime).ToUnixTimeSeconds();
        var sig = Sign(signingKey, u, exp);

        return $"{baseUrl.TrimEnd('/')}/hls?u={u}&exp={exp}&sig={sig}";
    }

    public static string Sign(string signingKey, string u, long exp)
        => Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes($"{u}|{exp}")));

    public static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
