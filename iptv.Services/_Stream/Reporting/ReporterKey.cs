using System.Security.Cryptography;
using System.Text;

namespace iptv.Services._Stream.Reporting;

/// <summary>
/// Anonymous reporter identity for Stream/ReportFailureAsync: SHA256(clientIp + daily salt).
/// The raw IP is never stored; the salt changes every UTC day, so keys cannot be linked across days.
/// </summary>
public static class ReporterKey
{
    public const string Prefix = "ip:";

    /// <summary>First address of an X-Forwarded-For value ("client, proxy1, proxy2").</summary>
    public static string FirstForwardedAddress(string forwardedOrIp)
    {
        if (string.IsNullOrWhiteSpace(forwardedOrIp))
            return null;

        var first = forwardedOrIp.Split(',')[0].Trim();
        return first.Length == 0 ? null : first;
    }

    public static string DailySalt(byte[] secret, DateTime utcNow)
        => Convert.ToHexString(HMACSHA256.HashData(secret,
            Encoding.UTF8.GetBytes(utcNow.ToUniversalTime().ToString("yyyy-MM-dd"))));

    public static string FromIp(string clientIp, byte[] secret, DateTime utcNow)
    {
        var ip = FirstForwardedAddress(clientIp) ?? "unknown";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ip + DailySalt(secret, utcNow)));
        return Prefix + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Secret from configuration, else derived from another server-side secret.</summary>
    public static byte[] ResolveSecret(string configured, string fallbackServerSecret)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Encoding.UTF8.GetBytes(configured);

        return HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(fallbackServerSecret ?? string.Empty),
            Encoding.UTF8.GetBytes("rimatv:report-ip-hash:v1"));
    }
}
