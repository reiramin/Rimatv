using iptv.Services._Common;
using iptv.Services._Common.Settings;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Utilities.Exceptions;

namespace iptv.Api.Utilities.Filters;

/// <summary>
/// Additional per-client-IP limit for anonymous endpoints, applied on top of [CustomRateLimit]
/// (which stays unchanged). Keyed by <see cref="ClientIp.Resolve"/> (right-most X-Forwarded-For
/// address after the trusted hops; the left-most one is spoofable). Fixed window: at most
/// <see cref="MaxRequests"/> per <see cref="PeriodSeconds"/>.
///
/// Memory on the 512 MB instance stays bounded: one small counter per (action, IP) in a dedicated
/// cache with a size limit and an absolute expiry — no static dictionary that grows with every client.
/// When the cache is full, new clients are not tracked (fail open) until entries expire.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class IpRateLimitAttribute(int maxRequests, int periodSeconds) : ActionFilterAttribute
{
    private const int MaxTrackedClients = 20_000;

    private static readonly MemoryCache Counters = new(new MemoryCacheOptions
    {
        SizeLimit = MaxTrackedClients,
        ExpirationScanFrequency = TimeSpan.FromMinutes(1)
    });

    public int MaxRequests { get; } = maxRequests;
    public int PeriodSeconds { get; } = periodSeconds;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var hops = (http.RequestServices?.GetService(typeof(ProxySettings)) as ProxySettings)?.TrustedHops ?? 0;
        var ip = ClientIp.Resolve(http, hops) ?? "unknown";
        var key = $"{context.ActionDescriptor.DisplayName}|{ip}";

        // GetOrCreate is not atomic; two first requests racing may each start a window. That can
        // only under-count by one, which is acceptable for a coarse abuse limit.
        var counter = Counters.GetOrCreate(key, entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(PeriodSeconds);
            return new Counter();
        })!;

        if (Interlocked.Increment(ref counter.Count) > MaxRequests)
        {
            http.Response.Headers.RetryAfter = PeriodSeconds.ToString();
            throw new TooManyRequestsException("Too many requests from this network. Try again later.");
        }

        await next();
    }

    private sealed class Counter
    {
        public int Count;
    }
}
