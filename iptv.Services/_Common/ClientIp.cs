using System.Net;
using Microsoft.AspNetCore.Http;

namespace iptv.Services._Common;

/// <summary>
/// The ONE rule for "who is the client" (rate limits, reporter keys, the resolver).
///
/// X-Forwarded-For is "client, proxy1, proxy2": every proxy APPENDS the address it received the
/// request from. The LEFT-most entry is whatever the client chose to send, so it is trivially
/// spoofable (a fresh fake value per request would dodge rate limits and inflate distinct-reporter
/// counts). The RIGHT-most entry was appended by our own edge proxy (Render) and is the address
/// that actually connected to it. With extra trusted proxies in front (config Proxy:TrustedHops,
/// e.g. 1 for a CDN), skip that many entries from the right.
/// </summary>
public static class ClientIp
{
    public static string Resolve(HttpContext context, int trustedHops = 0)
    {
        var remote = context?.Connection.RemoteIpAddress?.ToString();
        var header = context?.Request.Headers["X-Forwarded-For"].ToString();
        if (string.IsNullOrWhiteSpace(header))
            return remote;

        var entries = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var index = entries.Length - 1 - Math.Max(0, trustedHops);
        if (index < 0)
            return remote;   // fewer entries than trusted hops: nothing trustworthy in the header

        return IPAddress.TryParse(entries[index], out var ip) ? ip.ToString() : remote;
    }
}
