using System.Security.Cryptography;
using System.Text;

namespace iptv.Services._Stream.Reporting;

/// <summary>
/// Anonymous reporter identity for Stream/ReportFailureAsync: SHA256(clientIp + daily salt).
/// The raw IP is never stored; the salt changes every UTC day, so keys cannot be linked across days.
/// Each report also stores the key under the PREVIOUS day's salt, so one client reporting just
/// before and just after UTC midnight is still counted once (see <see cref="ReporterCounting"/>).
/// </summary>
public sealed record ReporterIdentity(string Key, string PreviousKey);

public static class ReporterKey
{
    public const string Prefix = "ip:";

    public static string DailySalt(byte[] secret, DateTime utcNow)
        => Convert.ToHexString(HMACSHA256.HashData(secret,
            Encoding.UTF8.GetBytes(utcNow.ToUniversalTime().ToString("yyyy-MM-dd"))));

    /// <param name="clientIp">Already resolved with <see cref="_Common.ClientIp.Resolve"/>.</param>
    public static string FromIp(string clientIp, byte[] secret, DateTime utcNow)
    {
        var ip = string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp.Trim();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ip + DailySalt(secret, utcNow)));
        return Prefix + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Keys of this client under today's and yesterday's salt.</summary>
    public static ReporterIdentity For(string clientIp, byte[] secret, DateTime utcNow)
        => new(FromIp(clientIp, secret, utcNow), FromIp(clientIp, secret, utcNow.AddDays(-1)));

    /// <summary>
    /// Secret from configuration, else derived from another server-side secret; null when neither
    /// exists (the caller then uses a random per-process secret — never a public constant).
    /// </summary>
    public static byte[] ResolveSecret(string configured, string fallbackServerSecret)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Encoding.UTF8.GetBytes(configured);

        if (string.IsNullOrWhiteSpace(fallbackServerSecret))
            return null;

        return HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(fallbackServerSecret),
            Encoding.UTF8.GetBytes("rimatv:report-ip-hash:v1"));
    }
}
