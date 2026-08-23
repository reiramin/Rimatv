using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._IptvProvider.Contracts;
using iptv.Services._IptvProvider.DTOs.Results;
using iptv.Services._IptvProvider.DTOs.Updates;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Net.Http.Json;
using iptv.Services._IptvProvider.DTOs.Settings;
using Utilities.Constants;
using Utilities.Exceptions.Common;
using Utilities.Models.Updates;
using Utilities.MongoDatabase.Extensions;
using Utilities.MongoDatabase.Filter;

namespace iptv.Services._IptvProvider;

public class IptvProviderService(
    IIptvProviderRepository _iptvProviderRepository,
    IHttpClientFactory _httpClientFactory)
    : IIptvProviderService, RegisterMode.IScopedDependency
{
    public async Task<List<ExternalChannel>> FetchChannelsAsync(
        IptvProviders provider,
        CancellationToken cancellationToken)
    {
        using var httpClient = CreateHttpClient(provider);

        return provider.Kind switch
        {
            ProviderKind.Pluto => await FetchPlutoChannelsAsync(httpClient, provider, cancellationToken),
            _ => await FetchGenericChannelsAsync(httpClient, provider, cancellationToken)
        };
    }

    public async Task<List<ExternalStream>> FetchStreamsAsync(
        IptvProviders provider,
        CancellationToken cancellationToken)
    {
        if (provider.Kind == ProviderKind.Pluto)
            return await FetchPlutoStreamsAsync(provider, cancellationToken);

        if (string.IsNullOrWhiteSpace(provider.StreamsEndpoint))
            return [];

        using var httpClient = CreateHttpClient(provider);

        var streams = await httpClient.GetFromJsonAsync<List<ExternalStream>>(
            provider.StreamsEndpoint,
            cancellationToken);

        return streams ?? [];
    }

    public async Task<List<ExternalLogo>> FetchLogosAsync(
        IptvProviders provider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider.LogosEndpoint))
            return [];

        using var httpClient = CreateHttpClient(provider);

        var logos = await httpClient.GetFromJsonAsync<List<ExternalLogo>>(
            provider.LogosEndpoint,
            cancellationToken);

        return logos ?? [];
    }

    public async Task<List<IptvProviders>> GetActiveProvidersAsync(
        CancellationToken cancellationToken = default)
        => await _iptvProviderRepository.AsQueryable()
            .Where(q => !q.Inactive)
            .ToListAsync(cancellationToken);

    public async Task<IptvProviderFilteredResult> CreateAsync(
        IptvProviderCreateUpdate update)
    {
        if (await _iptvProviderRepository.AsQueryable()
                .AnyAsync(q => q.Name == update.Name && q.BaseUrl == update.BaseUrl))
            throw new BadRequestException(
                "An IPTV provider with this name already exists.");

        ValidateUrl(nameof(update.BaseUrl), update.BaseUrl);
        ValidateUrl(nameof(update.ChannelsEndpoint), update.ChannelsEndpoint);
        ValidateUrl(nameof(update.StreamsEndpoint), update.StreamsEndpoint, isRequired: false);
        ValidateUrl(nameof(update.LogosEndpoint), update.LogosEndpoint, isRequired: false);

        var provider = new IptvProviders
        {
            Name = update.Name,
            BaseUrl = update.BaseUrl,
            ChannelsEndpoint = update.ChannelsEndpoint,
            StreamsEndpoint = update.StreamsEndpoint,
            LogosEndpoint = update.LogosEndpoint,
            ApiKey = update.ApiKey,
            Inactive = update.Inactive,
            Kind = update.Kind,
            // UseProxy = update.UseProxy
        };

        await _iptvProviderRepository.InsertOneAsync(provider);

        return MapToResult(provider);
    }

    public async Task<IptvProviderFilteredResult> EditAsync(
        IptvProviderEditUpdate update)
    {
        var provider =
            await _iptvProviderRepository.GetByPublicKeyAsync(
                update.PublicKey) ?? throw new NotFoundException("IPTV provider not found.");

        if (await _iptvProviderRepository.AsQueryable()
                .AnyAsync(q =>
                    q.Name == update.Name &&
                    q.PublicKey != update.PublicKey))
            throw new BadRequestException(
                "An IPTV provider with this name already exists.");

        if (!string.IsNullOrWhiteSpace(update.ApiKey))
            provider.ApiKey = update.ApiKey;
        if (!string.IsNullOrWhiteSpace(update.BaseUrl))
        {
            ValidateUrl(nameof(update.BaseUrl), update.BaseUrl);
            provider.BaseUrl = update.BaseUrl;
        }
        if (!string.IsNullOrWhiteSpace(update.ChannelsEndpoint))
        {
            ValidateUrl(nameof(update.ChannelsEndpoint), update.ChannelsEndpoint);
            provider.ChannelsEndpoint = update.ChannelsEndpoint;
        }
        if (!string.IsNullOrWhiteSpace(update.StreamsEndpoint))
        {
            ValidateUrl(nameof(update.StreamsEndpoint), update.StreamsEndpoint);
            provider.StreamsEndpoint = update.StreamsEndpoint;
        }
        if (!string.IsNullOrWhiteSpace(update.LogosEndpoint))
        {
            ValidateUrl(nameof(update.LogosEndpoint), update.LogosEndpoint);
            provider.LogosEndpoint = update.LogosEndpoint;
        }

        provider.Inactive = update.Inactive;

        if (!string.IsNullOrWhiteSpace(update.ApiKey))
            provider.ApiKey = update.ApiKey;

        await _iptvProviderRepository.ReplaceOneAsync(provider);

        return MapToResult(provider);
    }

    public async Task<IptvProviderFilteredResult> GetByPublicKeyAsync(GetGlobalIdUpdate publicKey)
    {
        var provider =
            await _iptvProviderRepository.GetByPublicKeyAsync(publicKey.Id);
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
        string publicKey,
        bool shouldActivate)
    {
        var provider =
            await _iptvProviderRepository.GetByPublicKeyAsync(publicKey);

        provider.Inactive = !shouldActivate;

        var update = Builders<IptvProviders>.Update
            .Set(q => q.Inactive, provider.Inactive);

        await _iptvProviderRepository.FindOneAndUpdateAsync(
            q => q.PublicKey == publicKey,
            update,
            CancellationToken.None);

        return MapToResult(provider);
    }

    public async Task<string> DeleteAsync(string publicKey)
    {
        var provider =
            await _iptvProviderRepository.GetByPublicKeyAsync(publicKey);

        await _iptvProviderRepository.DeleteOneAsync(q => q.PublicKey == provider.PublicKey);

        return provider.PublicKey;
    }

    #region Private Methods

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
        {
            throw new BadRequestException(
                $"{fieldName} is not a valid absolute URL (must start with http:// or https://): '{value}'");
        }
    }

    private static async Task<List<ExternalChannel>> FetchGenericChannelsAsync(
        HttpClient httpClient, IptvProviders provider, CancellationToken cancellationToken)
    {
        var channels = await httpClient.GetFromJsonAsync<List<ExternalChannel>>(
            provider.ChannelsEndpoint, cancellationToken);

        return channels ?? [];
    }

    private HttpClient CreateHttpClient(IptvProviders provider)
    {
        // var clientName = provider.UseProxy ? "IptvProvider-Proxied" : "IptvProvider";
        var httpClient = _httpClientFactory.CreateClient("IptvProvider");

        if (!string.IsNullOrWhiteSpace(provider.BaseUrl))
            httpClient.BaseAddress = new Uri(provider.BaseUrl);

        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", provider.ApiKey);

        if (!httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        return httpClient;
    }

    private static IptvProviderFilteredResult MapToResult(
        IptvProviders provider)
    {
        return new IptvProviderFilteredResult
        {
            PublicKey = provider.PublicKey,
            Name = provider.Name,
            BaseUrl = provider.BaseUrl,
            ChannelsEndpoint = provider.ChannelsEndpoint,
            StreamsEndpoint = provider.StreamsEndpoint,
            LogosEndpoint = provider.LogosEndpoint,
            Inactive = provider.Inactive,
            LastSyncMoment = provider.LastSyncMoment,
            LastSyncStatus = provider.LastSyncStatus,
            CreatedByInfo = provider.CreatedByInfo,
            CreatedMoment = provider.CreatedMoment,
            ModifiedByInfo = provider.ModifiedByInfo,
            ModifiedMoment = provider.ModifiedMoment
        };
    }

    #endregion

    #region Private Methods - Pluto

    private static readonly Dictionary<string, (DateTime FetchedAt, List<PlutoChannel> Data)> _plutoCache = new();
    private static readonly SemaphoreSlim _plutoCach‍eLock = new(1, 1);
    private static readonly TimeSpan PlutoCacheTtl = TimeSpan.FromMinutes(20);

    private async Task<List<PlutoChannel>> FetchPlutoRawAsync(
        IptvProviders provider, CancellationToken cancellationToken)
    {
        await _plutoCacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_plutoCache.TryGetValue(provider.PublicKey, out var cached) &&
                DateTime.UtcNow - cached.FetchedAt < PlutoCacheTtl)
                return cached.Data;

            using var httpClient = CreateHttpClient(provider);

            // آدرس واقعی Pluto برای لیست کانال‌ها؛ می‌تونی provider.ChannelsEndpoint رو
            // دقیقاً همین مقدار بذاری (مثلاً "https://api.pluto.tv/v2/channels.json")
            var raw = await httpClient.GetFromJsonAsync<List<PlutoChannel>>(
                provider.ChannelsEndpoint, cancellationToken) ?? [];

            _plutoCache[provider.PublicKey] = (DateTime.UtcNow, raw);
            return raw;
        }
        finally
        {
            _plutoCacheLock.Release();
        }
    }

    private async Task<List<ExternalChannel>> FetchPlutoChannelsAsync(
        HttpClient httpClient, IptvProviders provider, CancellationToken cancellationToken)
    {
        var raw = await FetchPlutoRawAsync(provider, cancellationToken);

        return raw
            .Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Name))
            .Select(c => new ExternalChannel
            {
                Id = c.Id,
                Name = c.Name,
                Logo = c.Logo?.Path,
                Categories = string.IsNullOrWhiteSpace(c.Category) ? [] : [c.Category],
                Country = "US"
            })
            .ToList();
    }

    private async Task<List<ExternalStream>> FetchPlutoStreamsAsync(
        IptvProviders provider, CancellationToken cancellationToken)
    {
        var raw = await FetchPlutoRawAsync(provider, cancellationToken);

        return raw
            .Where(c => !string.IsNullOrWhiteSpace(c.Id) &&
                        c.Stitched?.Urls != null &&
                        c.Stitched.Urls.Count > 0)
            .Select(c =>
            {
                var bestUrl = c.Stitched.Urls.FirstOrDefault(u => u.Type == "hls")
                              ?? c.Stitched.Urls.First();

                return new ExternalStream
                {
                    Channel = c.Id,
                    Url = bestUrl.Url,
                    Quality = "hls"
                };
            })
            .ToList();
    }

    #endregion
}