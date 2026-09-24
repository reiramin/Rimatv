using System.Collections.Concurrent;
using System.Net;
using System.Text;
using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._Resolver.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Utilities.Constants;
using Utilities.Exceptions.Common;

namespace iptv.Services._Resolver.Contracts
{
    public interface IStreamResolverService
    {
        Task<StreamResolveResult> ResolveAsync(string streamId, string clientIp, CancellationToken cancellationToken = default);
    }
}

namespace iptv.Services._Resolver
{
    using Contracts;

    /// <summary>
    /// On-demand resolution of official tokenized streams (type "resolve"). The page/API is fetched
    /// server-side (inbound traffic only — no video byte ever passes through the backend), the .m3u8
    /// is extracted and verified to start with #EXTM3U. Results live in a DEDICATED IMemoryCache
    /// (300 entries) for min(ttlSeconds, tokenExpiry − 60 s, 15 min); failures and unknown stream ids
    /// for 60 s. Concurrent requests for one stream share a single resolution (single-flight) and at
    /// most <see cref="MaxConcurrentUpstream"/> upstream fetches run at once.
    /// </summary>
    public class StreamResolverService(
        IStreamRepository _streamRepository,
        IHttpClientFactory _httpClientFactory,
        ILogger<StreamResolverService> _logger)
        : IStreamResolverService, RegisterMode.IScopedDependency
    {
        public const string HttpClientName = "Resolver";
        public const int MaxConcurrentUpstream = 4;

        private const int MaxBodyBytes = 2 * 1024 * 1024;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan FailureCache = TimeSpan.FromSeconds(60);

        private static readonly MemoryCache Cache = new(new MemoryCacheOptions { SizeLimit = 300 });
        private static readonly ConcurrentDictionary<string, Lazy<Task<Outcome>>> InFlight = new(StringComparer.Ordinal);
        private static readonly SemaphoreSlim Upstream = new(MaxConcurrentUpstream, MaxConcurrentUpstream);

        private enum OutcomeKind { Result, NotFound, BadRequest }

        private sealed record Outcome(OutcomeKind Kind, StreamResolveResult Result = null);

        public async Task<StreamResolveResult> ResolveAsync(
            string streamId, string clientIp, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(streamId))
                throw new BadRequestException("streamId is required.");

            if (!Cache.TryGetValue(streamId, out Outcome outcome))
            {
                // Single-flight: the first caller starts the resolution, the others await it. The
                // shared work is not tied to any one caller's cancellation token.
                Lazy<Task<Outcome>> lazy = null;
                lazy = new Lazy<Task<Outcome>>(() => ResolveAndCacheAsync(streamId, () => InFlight.TryRemove(new(streamId, lazy))));
                outcome = await InFlight.GetOrAdd(streamId, lazy).Value.WaitAsync(cancellationToken);
            }

            return outcome.Kind switch
            {
                OutcomeKind.NotFound => throw new NotFoundException("Stream not found."),
                OutcomeKind.BadRequest => throw new BadRequestException("This stream does not need resolving."),
                _ => outcome.Result
            };
        }

        private async Task<Outcome> ResolveAndCacheAsync(string streamId, Action removeInFlight)
        {
            try
            {
                var stream = await _streamRepository.GetByStreamIdAsync(streamId);
                if (stream == null || stream.Inactive || stream.AdminDisabled)
                    return Remember(streamId, new Outcome(OutcomeKind.NotFound), FailureCache);
                if (stream.Type != StreamTypes.Resolve)
                    return Remember(streamId, new Outcome(OutcomeKind.BadRequest), FailureCache);

                // Declared IP-bound: a server-side token would never play for the user; answer before
                // any upstream fetch (the channel's clientResolve / officialPlayer / youtube rungs remain).
                if (stream.IpBound)
                    return Remember(streamId, new Outcome(OutcomeKind.Result,
                        Failure(ResolveErrorCodes.IpBound, stream.RequiredRegion)), FailureCache);

                var now = DateTime.UtcNow;
                StreamResolveResult result;
                try
                {
                    result = await ResolveCoreAsync(stream, now, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Resolve failed for stream {StreamId} ({PageUrl}).", stream.StreamId, stream.PageUrl);
                    result = Failure(ResolveErrorCodes.ResolveFailed, stream.RequiredRegion);
                }

                var ttl = result.Found
                    ? ResolverExtraction.CacheDuration(stream.ResolveTtlSeconds, result.ExpiresAt, now)
                    : FailureCache;
                return Remember(streamId, new Outcome(OutcomeKind.Result, result), ttl);
            }
            finally
            {
                removeInFlight();
            }
        }

        private static Outcome Remember(string streamId, Outcome outcome, TimeSpan ttl)
        {
            if (ttl > TimeSpan.Zero)
                Cache.Set(streamId, outcome, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = ttl });
            return outcome;
        }

        private async Task<StreamResolveResult> ResolveCoreAsync(Streams stream, DateTime now, CancellationToken ct)
        {
            var headers = stream.ResolveHeaders ?? [];
            var sourceUrl = !string.IsNullOrWhiteSpace(stream.ResolveApiUrl) ? stream.ResolveApiUrl : stream.PageUrl ?? stream.StreamUri;

            var (status, body) = await GetAsync(sourceUrl, headers, ct);
            if (IsRegionBlock(status))
                return Failure(ResolveErrorCodes.UseVpn, stream.RequiredRegion);
            if (status is < 200 or >= 300 || body == null)
                return Failure(ResolveErrorCodes.ResolveFailed, stream.RequiredRegion);

            var url = ResolverExtraction.ExtractUrl(
                body, stream.ResolveMethod, stream.ResolvePattern, stream.ResolveBaseUrl ?? sourceUrl);
            if (url == null)
                return Failure(ResolveErrorCodes.ResolveFailed, stream.RequiredRegion);

            // Do not hand out a token issued to OUR address: it would not play for the user. The
            // client falls back to the clientResolve / officialPlayer rungs of the same channel.
            if (ResolverExtraction.IsIpBound(url, stream.IpBound))
                return Failure(ResolveErrorCodes.IpBound, stream.RequiredRegion);

            var (playlistStatus, playlist) = await GetAsync(url, headers, ct, maxBytes: 64 * 1024);
            if (IsRegionBlock(playlistStatus))
                return Failure(ResolveErrorCodes.UseVpn, stream.RequiredRegion);
            if (!ResolverExtraction.IsPlaylist(playlist))
                return Failure(ResolveErrorCodes.ResolveFailed, stream.RequiredRegion);

            var expiry = ResolverExtraction.ParseExpiry(url, now);
            var ttl = ResolverExtraction.CacheDuration(stream.ResolveTtlSeconds, expiry, now);

            return new StreamResolveResult
            {
                Found = true,
                Url = url,
                UserAgent = headers.GetValueOrDefault("User-Agent"),
                Referer = headers.GetValueOrDefault("Referer"),
                ExpiresAt = expiry ?? (ttl > TimeSpan.Zero ? now + ttl : null),
                RequiredRegion = stream.RequiredRegion
            };
        }

        private async Task<(int Status, string Body)> GetAsync(
            string url, Dictionary<string, string> headers, CancellationToken ct, int maxBytes = MaxBodyBytes)
        {
            await Upstream.WaitAsync(ct);   // global cap on concurrent upstream fetches
            try
            {
                using var client = _httpClientFactory.CreateClient(HttpClientName);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(RequestTimeout);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
                foreach (var (k, v) in headers)
                {
                    request.Headers.Remove(k);
                    request.Headers.TryAddWithoutValidation(k, v);
                }

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                await using var body = await response.Content.ReadAsStreamAsync(cts.Token);
                var bytes = await ReadBoundedAsync(body, maxBytes, cts.Token);
                return ((int)response.StatusCode, Encoding.UTF8.GetString(bytes));
            }
            finally
            {
                Upstream.Release();
            }
        }

        /// <summary>Reads at most <paramref name="maxBytes"/> into a buffer that grows with the body.</summary>
        internal static async Task<byte[]> ReadBoundedAsync(Stream body, int maxBytes, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int n;
            while (buffer.Length < maxBytes &&
                   (n = await body.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, maxBytes - buffer.Length)), ct)) > 0)
                buffer.Write(chunk, 0, n);
            return buffer.ToArray();
        }

        private static bool IsRegionBlock(int status)
            => status is (int)HttpStatusCode.Forbidden or 451;

        private static StreamResolveResult Failure(string code, string region)
            => new() { Found = false, ErrorCode = code, RequiredRegion = region };
    }
}
