using System.Collections.Concurrent;
using iptv.Services._Common;
using iptv.Services._Common.Settings;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Utilities.Exceptions;

namespace iptv.Api.Utilities.Filters;

/// <summary>
/// Additional per-client-IP limit for anonymous endpoints, applied on top of [CustomRateLimit]
/// (which stays unchanged). Keyed by <see cref="ClientIp.Resolve"/> (right-most X-Forwarded-For
/// address after the trusted hops; the left-most one is spoofable). Fixed window: at most <see cref="MaxRequests"/> per <see cref="PeriodSeconds"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class IpRateLimitAttribute(int maxRequests, int periodSeconds) : ActionFilterAttribute
{
    private static readonly ConcurrentDictionary<string, object> Locks = new();

    public int MaxRequests { get; } = maxRequests;
    public int PeriodSeconds { get; } = periodSeconds;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var cache = (IMemoryCache)http.RequestServices.GetService(typeof(IMemoryCache));
        var hops = (http.RequestServices.GetService(typeof(ProxySettings)) as ProxySettings)?.TrustedHops ?? 0;
        var ip = ClientIp.Resolve(http, hops) ?? "unknown";
        var key = $"iprl_{context.ActionDescriptor.DisplayName}_{ip}";

        bool allowed;
        lock (Locks.GetOrAdd(key, _ => new object()))
        {
            var window = cache.GetOrCreate(key, e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(PeriodSeconds);
                return new Counter();
            });
            allowed = ++window.Count <= MaxRequests;
        }

        if (!allowed)
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
