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
    /// (300 entries) for min(ttlSeconds, tokenExpiry − 60 s, 15 min).
    /// </summary>
    public class StreamResolverService(
        IStreamRepository _streamRepository,
        IHttpClientFactory _httpClientFactory,
        ILogger<StreamResolverService> _logger)
        : IStreamResolverService, RegisterMode.IScopedDependency
    {
        private const int MaxBodyBytes = 2 * 1024 * 1024;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan FailureCache = TimeSpan.FromSeconds(60);

        private static readonly MemoryCache Cache = new(new MemoryCacheOptions { SizeLimit = 300 });

        public async Task<StreamResolveResult> ResolveAsync(
            string streamId, string clientIp, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(streamId))
                throw new BadRequestException("streamId is required.");

            if (Cache.TryGetValue(streamId, out StreamResolveResult cached))
                return cached;

            var stream = await _streamRepository.GetByStreamIdAsync(streamId, cancellationToken);
            if (stream == null || stream.Inactive || stream.AdminDisabled)
                throw new NotFoundException("Stream not found.");
            if (stream.Type != StreamTypes.Resolve)
                throw new BadRequestException("This stream does not need resolving.");

            var now = DateTime.UtcNow;
            StreamResolveResult result;
            try
            {
                result = await ResolveCoreAsync(stream, clientIp, now, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Resolve failed for stream {StreamId} ({PageUrl}).", stream.StreamId, stream.PageUrl);
                result = Failure(ResolveErrorCodes.ResolveFailed, stream.RequiredRegion);
            }

            var ttl = result.Found
                ? ResolverExtraction.CacheDuration(stream.ResolveTtlSeconds, result.ExpiresAt, now)
                : FailureCache;
            if (ttl > TimeSpan.Zero)
                Cache.Set(streamId, result, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = ttl });

            return result;
        }

        private async Task<StreamResolveResult> ResolveCoreAsync(
            Streams stream, string clientIp, DateTime now, CancellationToken ct)
        {
            var headers = stream.ResolveHeaders ?? [];
            var sourceUrl = !string.IsNullOrWhiteSpace(stream.ResolveApiUrl) ? stream.ResolveApiUrl : stream.PageUrl ?? stream.StreamUri;

            var (status, body) = await GetAsync(sourceUrl, headers, ct);
            if (IsRegionBlock(status))
                return Failure(ResolveErrorCodes.UseVpn, stream.RequiredRegion);
            if (status is < 200 or >= 300 || body == null)
                return Failure(ResolveErrorCodes.ResolveFailed, stream.RequiredRegion);

            var url = ResolverExtraction.ExtractUrl(body, stream.ResolveMethod, stream.ResolvePattern, sourceUrl);
            if (url == null)
                return Failure(ResolveErrorCodes.ResolveFailed, stream.RequiredRegion);

            // Do not hand out a token issued to OUR address: it would not play for the user. The
            // client falls back to the clientResolve / officialPlayer rungs of the same channel.
            if (ResolverExtraction.IsIpBound(url, stream.IpBound, clientIp))
                return Failure(ResolveErrorCodes.IpBound, stream.RequiredRegion);

            var (playlistStatus, playlist) = await GetAsync(url, headers, ct, maxBytes: 64 * 1024);
            if (IsRegionBlock(playlistStatus))
                return Failure(ResolveErrorCodes.UseVpn, stream.RequiredRegion);
            if (!ResolverExtraction.IsPlaylist(playlist))
                return Failure(ResolveErrorCodes.ResolveFailed, stream.RequiredRegion);

            var expiry = ResolverExtraction.ParseExpiry(url);
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
            using var client = _httpClientFactory.CreateClient("Resolver");
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
            var status = (int)response.StatusCode;

            await using var s = await response.Content.ReadAsStreamAsync(cts.Token);
            var buffer = new byte[maxBytes];
            var read = 0;
            int n;
            while (read < buffer.Length && (n = await s.ReadAsync(buffer.AsMemory(read), cts.Token)) > 0)
                read += n;

            return (status, Encoding.UTF8.GetString(buffer, 0, read));
        }

        private static bool IsRegionBlock(int status)
            => status is (int)HttpStatusCode.Forbidden or 451;

        private static StreamResolveResult Failure(string code, string region)
            => new() { Found = false, ErrorCode = code, RequiredRegion = region };
    }
}
