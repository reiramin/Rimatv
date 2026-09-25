using System.Net;
using System.Net.Sockets;

namespace iptv.Services._Resolver;

public sealed class SsrfBlockedException(string host)
    : IOException($"Refusing to connect to '{host}': it resolves only to internal addresses.");

/// <summary>
/// SSRF protection for the resolver's named HttpClient ("Resolver"). The page URL, the extracted
/// stream URL and every redirect hop open their connections through <see cref="ConnectAsync"/>,
/// which resolves the host and connects only to public addresses — never loopback, private,
/// link-local (incl. cloud metadata 169.254.169.254), CGNAT, unspecified or multicast, over IPv4 and
/// IPv6 (IPv4-mapped IPv6 is checked as IPv4). Checking at connect time also defeats DNS rebinding.
/// </summary>
public static class SsrfGuard
{
    public const int MaxRedirects = 5;

    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = MaxRedirects,
        ConnectCallback = ConnectAsync,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(10)
    };

    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host.Trim('[', ']'), out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, ct);

        var allowed = addresses.Where(a => !IsBlocked(a)).ToList();
        if (allowed.Count == 0)
            throw new SsrfBlockedException(host);

        Exception last = null;
        foreach (var address in allowed)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                last = ex;
                if (ex is OperationCanceledException) throw;
            }
        }

        throw last ?? new SsrfBlockedException(host);
    }

    public static bool IsBlocked(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.Broadcast) || address.Equals(IPAddress.IPv6None))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 0 ||                                        // 0.0.0.0/8
                   b[0] == 10 ||                                       // 10/8
                   (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||        // 172.16/12
                   (b[0] == 192 && b[1] == 168) ||                     // 192.168/16
                   (b[0] == 169 && b[1] == 254) ||                     // link-local / metadata
                   (b[0] == 100 && b[1] >= 64 && b[1] <= 127) ||       // CGNAT 100.64/10
                   b[0] >= 224;                                        // multicast / reserved
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return address.IsIPv6LinkLocal ||                          // fe80::/10
                   address.IsIPv6SiteLocal ||                          // fec0::/10
                   address.IsIPv6Multicast ||                          // ff00::/8
                   (b[0] & 0xFE) == 0xFC;                              // fc00::/7 unique-local
        }

        return true;   // unknown families are never allowed
    }
}
