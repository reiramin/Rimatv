using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._Channel;
using iptv.Services._IptvProvider.Contracts;
using iptv.Services._IptvProvider.DTOs.Results;
using iptv.Services._IptvProvider.DTOs.Settings;
using iptv.Services._IptvProvider.DTOs.Updates;
using iptv.Services._IptvProvider.Fetchers;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Utilities.Constants;
using Utilities.Exceptions.Common;
using Utilities.Models.Updates;
using Utilities.MongoDatabase.Extensions;
using Utilities.MongoDatabase.Filter;

namespace iptv.Services._IptvProvider;

public class IptvProviderService(
    IIptvProviderRepository _iptvProviderRepository,
    IHttpClientFactory _httpClientFactory,
    IEnumerable<IProviderFetcher> _fetchers,
    IChannelRepository _channelRepository,
    IStreamRepository _streamRepository)
    : IIptvProviderService, RegisterMode.IScopedDependency
{
    public async Task<ProviderFetchResult> FetchAllAsync(
        IptvProviders provider, CancellationToken cancellationToken)
    {
        if (provider.Kind == ProviderKind.Pluto)
            return await BuildPlutoResultAsync(provider, cancellationToken);

        var fetcher = _fetchers.FirstOrDefault(f => f.Kind == provider.Kind)
                      ?? throw new BadRequestException(
                          $"No fetcher is registered for provider kind '{provider.Kind}'.");

        return await fetcher.FetchAsync(provider, cancellationToken);
    }

    public async Task<List<ExternalChannel>> FetchChannelsAsync(
        IptvProviders provider, CancellationToken cancellationToken)
        => (await FetchAllAsync(provider, cancellationToken)).Channels;

    public async Task<List<ExternalStream>> FetchStreamsAsync(
        IptvProviders provider, CancellationToken cancellationToken)
        => (await FetchAllAsync(provider, cancellationToken)).Streams;

    public async Task<List<ExternalLogo>> FetchLogosAsync(
        IptvProviders provider, CancellationToken cancellationToken)
        => (await FetchAllAsync(provider, cancellationToken)).Logos;

    public async Task<List<IptvProviders>> GetActiveProvidersAsync(
        CancellationToken cancellationToken = default)
        => await _iptvProviderRepository.AsQueryable()
            .Where(q => !q.Inactive)
            .ToListAsync(cancellationToken);

    public async Task<IptvProviderFilteredResult> CreateAsync(
        IptvProviderCreateUpdate update)
    {
        ValidateProviderConfiguration(update.Kind, update.ChannelsEndpoint, update.BaseUrl);
        ValidateEndpoints(update.StreamsEndpoint, update.LogosEndpoint, update.FeedsEndpoint,
            update.BlocklistEndpoint, update.FallbackBaseUrl, update.AdditionalEndpoints);

        if (await _iptvProviderRepository.AsQueryable()
                .AnyAsync(q => q.Name == update.Name))
            throw new BadRequestException(
                "An IPTV provider with this name already exists.");

        var provider = new IptvProviders
        {
            Name = update.Name,
            BaseUrl = update.BaseUrl,
            ChannelsEndpoint = update.ChannelsEndpoint,
            StreamsEndpoint = update.StreamsEndpoint,
            LogosEndpoint = update.LogosEndpoint,
            FeedsEndpoint = update.FeedsEndpoint,
            BlocklistEndpoint = update.BlocklistEndpoint,
            FallbackBaseUrl = update.FallbackBaseUrl,
            AdditionalEndpoints = update.AdditionalEndpoints ?? [],
            Headers = update.Headers ?? [],
            ApiKey = update.ApiKey,
            Inactive = update.Inactive,
            Kind = update.Kind,
            FetchTimeoutSeconds = update.FetchTimeoutSeconds,
            Priority = update.Priority
        };

        await _iptvProviderRepository.InsertOneAsync(provider);

        return MapToResult(provider);
    }

    public async Task<IptvProviderFilteredResult> EditAsync(
        IptvProviderEditUpdate update)
    {
        var provider =
            await _iptvProviderRepository.GetByPublicKeyAsync(update.PublicKey)
            ?? throw new NotFoundException("IPTV provider not found.");

        if (await _iptvProviderRepository.AsQueryable()
                .AnyAsync(q => q.Name == update.Name && q.PublicKey != update.PublicKey))
            throw new BadRequestException(
                "An IPTV provider with this name already exists.");

        provider.Name = update.Name;
        provider.Kind = update.Kind;
        provider.FetchTimeoutSeconds = update.FetchTimeoutSeconds;
        provider.Priority = update.Priority;

        if (!string.IsNullOrWhiteSpace(update.BaseUrl))
        {
            ValidateUrl(nameof(update.BaseUrl), update.BaseUrl);
            provider.BaseUrl = update.BaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(update.ChannelsEndpoint))
        {
            ValidateChannelsEndpoint(update.Kind, update.ChannelsEndpoint);
            provider.ChannelsEndpoint = update.ChannelsEndpoint;
        }

        ApplyOptionalUrl(update.StreamsEndpoint, v => provider.StreamsEndpoint = v);
        ApplyOptionalUrl(update.LogosEndpoint, v => provider.LogosEndpoint = v);
        ApplyOptionalUrl(update.FeedsEndpoint, v => provider.FeedsEndpoint = v);
        ApplyOptionalUrl(update.BlocklistEndpoint, v => provider.BlocklistEndpoint = v);
        ApplyOptionalUrl(update.FallbackBaseUrl, v => provider.FallbackBaseUrl = v);

        if (update.AdditionalEndpoints != null)
        {
            foreach (var e in update.AdditionalEndpoints.Where(e => !string.IsNullOrWhiteSpace(e)))
                ValidateUrl(nameof(update.AdditionalEndpoints), e);
            provider.AdditionalEndpoints = update.AdditionalEndpoints;
        }

        if (update.Headers != null)
            provider.Headers = update.Headers;

        if (!string.IsNullOrWhiteSpace(update.ApiKey))
            provider.ApiKey = update.ApiKey;

        var wasInactive = provider.Inactive;
        provider.Inactive = update.Inactive;

        await _iptvProviderRepository.ReplaceOneAsync(provider);

        // Editing the active state cascades to channels/streams.
        if (wasInactive != provider.Inactive)
            await CascadeProviderActiveStateAsync(provider.PublicKey, !provider.Inactive,
                CancellationToken.None);

        return MapToResult(provider);
    }

    public async Task<IptvProviderFilteredResult> GetByPublicKeyAsync(GetGlobalIdUpdate publicKey)
    {
        var provider =
            await _iptvProviderRepository.GetByPublicKeyAsync(publicKey.Id)
            ?? throw new NotFoundException("IPTV provider not found.");

        return MapToResult(provider);
    }

    public async Task<MonjoFilteredResult<IptvProviderFilteredResult>> GetAllAsync(
        MonjoQuery query)
    {
        query.WithBase<IptvProviderFilteredResult>();

        return await _iptvProviderRepository
            .AsQueryable()
            .Apply(query.Where, nameof(IptvProviderFilteredResult))
            .Apply(query.Order, nameof(IptvProviderFilteredResult))
            .Select(provider => new IptvProviderFilteredResult
            {
                PublicKey = provider.PublicKey,
                Name = provider.Name,
                BaseUrl = provider.BaseUrl,
                ChannelsEndpoint = provider.ChannelsEndpoint,
                StreamsEndpoint = provider.StreamsEndpoint,
                LogosEndpoint = provider.LogosEndpoint,
                FeedsEndpoint = provider.FeedsEndpoint,
                BlocklistEndpoint = provider.BlocklistEndpoint,
                FallbackBaseUrl = provider.FallbackBaseUrl,
                AdditionalEndpoints = provider.AdditionalEndpoints,
                Kind = provider.Kind,
                FetchTimeoutSeconds = provider.FetchTimeoutSeconds,
                Priority = provider.Priority,
                Inactive = provider.Inactive,
                LastSyncMoment = provider.LastSyncMoment,
                LastSyncStatus = provider.LastSyncStatus,
                CreatedByInfo = provider.CreatedByInfo,
                CreatedMoment = provider.CreatedMoment,
                ModifiedByInfo = provider.ModifiedByInfo,
                ModifiedMoment = provider.ModifiedMoment
            })
            .ExecuteAsync(query, nameof(IptvProviderFilteredResult));
    }

    public async Task<IptvProviderFilteredResult> ActivateAsync(
        string publicKey, bool shouldActivate)
    {
        var provider =
            await _iptvProviderRepository.GetByPublicKeyAsync(publicKey)
            ?? throw new NotFoundException("IPTV provider not found.");

        provider.Inactive = !shouldActivate;

        var update = Builders<IptvProviders>.Update
            .Set(q => q.Inactive, provider.Inactive);

        await _iptvProviderRepository.FindOneAndUpdateAsync(
            q => q.PublicKey == publicKey, update, CancellationToken.None);

        // Deactivating hides the provider's channels/streams from user-facing results; re-activating restores.
        await CascadeProviderActiveStateAsync(publicKey, shouldActivate, CancellationToken.None);

        return MapToResult(provider);
    }

    public async Task<string> DeleteAsync(string publicKey)
    {
        var provider =
            await _iptvProviderRepository.GetByPublicKeyAsync(publicKey)
            ?? throw new NotFoundException("IPTV provider not found.");

        // Deleting a provider deletes its channels and streams (soft delete via repository).
        await _streamRepository.DeleteManyAsync(q => q.ProviderPublicKey == provider.PublicKey);
        await _channelRepository.DeleteManyAsync(q => q.ProviderPublicKey == provider.PublicKey);
        await _iptvProviderRepository.DeleteOneAsync(q => q.PublicKey == provider.PublicKey);

        ChannelService.InvalidateSharedLiteCache();

        return provider.PublicKey;
    }

    #region Cascade

    private async Task CascadeProviderActiveStateAsync(
        string providerPublicKey, bool shouldActivate, CancellationToken cancellationToken)
    {
        var inactive = !shouldActivate;

        var channelUpdate = Builders<Channels>.Update
            .Set(q => q.Inactive, inactive)
            .Set(q => q.ModifiedMoment, DateTime.UtcNow);

        await _channelRepository.UpdateManyAsync(
            q => q.ProviderPublicKey == providerPublicKey, channelUpdate, cancellationToken);

        var streamUpdate = Builders<Streams>.Update
            .Set(q => q.Inactive, inactive)
            .Set(q => q.ModifiedMoment, DateTime.UtcNow);

        await _streamRepository.UpdateManyAsync(
            q => q.ProviderPublicKey == providerPublicKey, streamUpdate, cancellationToken);

        ChannelService.InvalidateSharedLiteCache();
    }

    #endregion

    #region Validation

    private static void ValidateProviderConfiguration(
        ProviderKind kind, string channelsEndpoint, string baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
            ValidateUrl(nameof(baseUrl), baseUrl);

        // Pluto has a built-in endpoint; Resolver reads an embedded resource.
        if (kind is ProviderKind.Pluto or ProviderKind.Resolver)
        {
            if (!string.IsNullOrWhiteSpace(channelsEndpoint))
                ValidateUrl(nameof(channelsEndpoint), channelsEndpoint);
            return;
        }

        if (string.IsNullOrWhiteSpace(channelsEndpoint))
            throw new BadRequestException("ChannelsEndpoint is required for this provider kind.");

        ValidateChannelsEndpoint(kind, channelsEndpoint);
    }

    private static void ValidateChannelsEndpoint(ProviderKind kind, string channelsEndpoint)
    {
        ValidateUrl(nameof(channelsEndpoint), channelsEndpoint);

        // M3U providers legitimately point at .m3u / .m3u8 playlists; other JSON kinds must not.
        if (kind is not ProviderKind.M3u &&
            channelsEndpoint.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException(
                $"A {kind} provider ChannelsEndpoint cannot be an M3U8 stream URL.");
    }

    private static void ValidateEndpoints(
        string streams, string logos, string feeds, string blocklist,
        string fallbackBaseUrl, List<string> additional)
    {
        ValidateUrl(nameof(streams), streams, isRequired: false);
        ValidateUrl(nameof(logos), logos, isRequired: false);
        ValidateUrl(nameof(feeds), feeds, isRequired: false);
        ValidateUrl(nameof(blocklist), blocklist, isRequired: false);
        ValidateUrl(nameof(fallbackBaseUrl), fallbackBaseUrl, isRequired: false);

        if (additional == null) return;
        foreach (var e in additional.Where(e => !string.IsNullOrWhiteSpace(e)))
            ValidateUrl(nameof(additional), e);
    }

    private static void ApplyOptionalUrl(string value, Action<string> assign)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        ValidateUrl("endpoint", value);
        assign(value);
    }

    private static void ValidateUrl(string fieldName, string value, bool isRequired = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (isRequired)
                throw new BadRequestException($"{fieldName} is required.");
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new BadRequestException(
                $"{fieldName} is not a valid absolute URL (must start with http:// or https://): '{value}'");
    }

    #endregion

    #region Helpers

    private HttpClient CreateHttpClient(IptvProviders provider)
    {
        var httpClient = _httpClientFactory.CreateClient("IptvProvider");

        if (!string.IsNullOrWhiteSpace(provider.BaseUrl))
            httpClient.BaseAddress = new Uri(provider.BaseUrl);

        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", provider.ApiKey);

        if (!httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        return httpClient;
    }

    private static IptvProviderFilteredResult MapToResult(IptvProviders provider)
        => new()
        {
            PublicKey = provider.PublicKey,
            Name = provider.Name,
            BaseUrl = provider.BaseUrl,
            ChannelsEndpoint = provider.ChannelsEndpoint,
            StreamsEndpoint = provider.StreamsEndpoint,
            LogosEndpoint = provider.LogosEndpoint,
            FeedsEndpoint = provider.FeedsEndpoint,
            BlocklistEndpoint = provider.BlocklistEndpoint,
            FallbackBaseUrl = provider.FallbackBaseUrl,
            AdditionalEndpoints = provider.AdditionalEndpoints,
            Kind = provider.Kind,
            FetchTimeoutSeconds = provider.FetchTimeoutSeconds,
            Priority = provider.Priority,
            Inactive = provider.Inactive,
            LastSyncMoment = provider.LastSyncMoment,
            LastSyncStatus = provider.LastSyncStatus,
            CreatedByInfo = provider.CreatedByInfo,
            CreatedMoment = provider.CreatedMoment,
            ModifiedByInfo = provider.ModifiedByInfo,
            ModifiedMoment = provider.ModifiedMoment
        };

    #endregion

    #region Pluto (cache MUST NOT be modified — hard rule)

    private static readonly Dictionary<
            string,
            (DateTime FetchedAt, List<PlutoChannel> Data)>
        _plutoCache = new();

    private static readonly SemaphoreSlim
        _plutoCacheLock = new(1, 1);

    private static readonly TimeSpan
        PlutoCacheTtl = TimeSpan.FromMinutes(20);

    private async Task<List<PlutoChannel>> FetchPlutoRawAsync(
        IptvProviders provider,
        CancellationToken cancellationToken)
    {
        await _plutoCacheLock.WaitAsync(cancellationToken);

        try
        {
            if (_plutoCache.TryGetValue(provider.PublicKey, out var cached)
                && DateTime.UtcNow - cached.FetchedAt < PlutoCacheTtl)
            {
                return cached.Data;
            }

            using var httpClient = CreateHttpClient(provider);

            var raw =
                await httpClient.GetFromJsonAsync<List<PlutoChannel>>(
                    provider.ChannelsEndpoint, cancellationToken)
                ?? [];

            _plutoCache[provider.PublicKey] = (DateTime.UtcNow, raw);

            return raw;
        }
        finally
        {
            _plutoCacheLock.Release();
        }
    }

    private async Task<ProviderFetchResult> BuildPlutoResultAsync(
        IptvProviders provider, CancellationToken cancellationToken)
    {
        var raw = await FetchPlutoRawAsync(provider, cancellationToken);

        var result = new ProviderFetchResult();

        // A single UUID reused for every channel in this sync (stable per sync).
        var deviceId = DeterministicUuid(provider.PublicKey);

        foreach (var channel in raw)
        {
            if (string.IsNullOrWhiteSpace(channel.Id) || string.IsNullOrWhiteSpace(channel.Name))
                continue;

            var masterUrl =
                channel.Stitched?.Urls?
                    .FirstOrDefault(u => string.Equals(u.Type, "hls", StringComparison.OrdinalIgnoreCase))?.Url
                ?? channel.Stitched?.Urls?.FirstOrDefault()?.Url;

            if (string.IsNullOrWhiteSpace(masterUrl))
                continue;

            result.Channels.Add(new ExternalChannel
            {
                Id = channel.Id,
                Name = channel.Name,
                Logo = channel.Logo?.Path,
                Categories = string.IsNullOrWhiteSpace(channel.Category) ? [] : [channel.Category],
                Country = "US"
            });

            // Store the master URL as ONE adaptive "Auto" stream; do not extract per-variant
            // session URLs (they expire). Stable ExternalId keeps the same doc across syncs.
            result.Streams.Add(new ExternalStream
            {
                Channel = channel.Id,
                Url = FillPlutoSessionParams(masterUrl, deviceId),
                Quality = "Auto",
                IsAdaptive = true,
                StableExternalId = ComputeHash(provider.PublicKey, channel.Id, "Auto")
            });
        }

        return result;
    }

    // Fills empty deviceId/sid (and advertisingId) query parameters with a stable UUID.
    private static string FillPlutoSessionParams(string url, string uuid)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        var query = uri.Query.TrimStart('?');
        if (query.Length == 0)
            return url;

        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var eq = parts[i].IndexOf('=');
            if (eq < 0) continue;

            var key = parts[i][..eq];
            var value = parts[i][(eq + 1)..];

            if (value.Length == 0 &&
                key is "deviceId" or "sid" or "advertisingId" or "sessionID" or "clientID")
                parts[i] = $"{key}={uuid}";
        }

        var builder = new UriBuilder(uri) { Query = string.Join('&', parts) };
        return builder.Uri.ToString();
    }

    private static string DeterministicUuid(string seed)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(bytes).ToString();
    }

    private static string ComputeHash(params string[] values)
    {
        var raw = string.Join("|", values.Select(v => v ?? string.Empty));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    #endregion
}
