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

        return await FetchLargeJsonAsync<ExternalStream>(
            httpClient, provider, provider.StreamsEndpoint, cancellationToken);
    }

    public async Task<List<ExternalLogo>> FetchLogosAsync(
        IptvProviders provider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider.LogosEndpoint))
            return [];

        using var httpClient = CreateHttpClient(provider);

        return await FetchLargeJsonAsync<ExternalLogo>(
            httpClient, provider, provider.LogosEndpoint, cancellationToken);
    }

    public async Task<List<IptvProviders>> GetActiveProvidersAsync(
        CancellationToken cancellationToken = default)
        => await _iptvProviderRepository.AsQueryable()
            .Where(q => !q.Inactive)
            .ToListAsync(cancellationToken);

    public async Task<IptvProviderFilteredResult> CreateAsync(
        IptvProviderCreateUpdate update)
    {
        ValidateProviderConfiguration(update);
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
                update.PublicKey)
            ?? throw new NotFoundException(
                "IPTV provider not found.");

        if (await _iptvProviderRepository
                .AsQueryable()
                .AnyAsync(q =>
                    q.Name == update.Name &&
                    q.PublicKey != update.PublicKey))
        {
            throw new BadRequestException(
                "An IPTV provider with this name already exists.");
        }

        provider.Name = update.Name;

        if (!string.IsNullOrWhiteSpace(update.BaseUrl))
        {
            ValidateUrl(
                nameof(update.BaseUrl),
                update.BaseUrl);

            provider.BaseUrl = update.BaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(update.ChannelsEndpoint))
        {
            ValidateUrl(
                nameof(update.ChannelsEndpoint),
                update.ChannelsEndpoint);

            provider.ChannelsEndpoint =
                update.ChannelsEndpoint;
        }

        if (!string.IsNullOrWhiteSpace(update.StreamsEndpoint))
        {
            ValidateUrl(
                nameof(update.StreamsEndpoint),
                update.StreamsEndpoint);

            provider.StreamsEndpoint =
                update.StreamsEndpoint;
        }
        else if (provider.Kind == ProviderKind.Generic)
        {
            provider.StreamsEndpoint = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(update.LogosEndpoint))
        {
            ValidateUrl(
                nameof(update.LogosEndpoint),
                update.LogosEndpoint);

            provider.LogosEndpoint =
                update.LogosEndpoint;
        }
        else if (provider.Kind == ProviderKind.Generic)
        {
            provider.LogosEndpoint = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(update.ApiKey))
        {
            provider.ApiKey = update.ApiKey;
        }

        provider.Inactive = update.Inactive;

        await _iptvProviderRepository
            .ReplaceOneAsync(provider);

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

    private static readonly System.Text.Json.JsonSerializerOptions ExternalJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static void ValidateProviderConfiguration(
        IptvProviderCreateUpdate update)
    {
        if (update.Kind == ProviderKind.Pluto)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(update.ChannelsEndpoint))
        {
            throw new BadRequestException(
                "ChannelsEndpoint is required for Generic providers.");
        }

        if (update.ChannelsEndpoint.Contains(
                ".m3u8",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "Generic provider ChannelsEndpoint cannot be an M3U8 stream URL.");
        }
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
        {
            throw new BadRequestException(
                $"{fieldName} is not a valid absolute URL (must start with http:// or https://): '{value}'");
        }
    }

    private static async Task<List<ExternalChannel>> FetchGenericChannelsAsync(
        HttpClient httpClient, IptvProviders provider, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        Exception lastError = null;

        var timeoutSeconds = provider.FetchTimeoutSeconds > 0
            ? provider.FetchTimeoutSeconds
            : 60;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                var channels = await httpClient.GetFromJsonAsync<List<ExternalChannel>>(
                    provider.ChannelsEndpoint, ExternalJsonOptions, cts.Token);

                return channels ?? [];
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(attempt * 5), cancellationToken);
            }
        }

        throw lastError!;
    }

    private static async Task<List<T>> FetchLargeJsonAsync<T>(
        HttpClient httpClient,
        IptvProviders provider,
        string url,
        CancellationToken cancellationToken)
    {
        using var requestCts = provider.FetchTimeoutSeconds > 0
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        if (requestCts != null)
            requestCts.CancelAfter(TimeSpan.FromSeconds(provider.FetchTimeoutSeconds));

        var effectiveToken = requestCts?.Token ?? cancellationToken;

        using var response = await httpClient.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, effectiveToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(effectiveToken);

        var result = await System.Text.Json.JsonSerializer.DeserializeAsync<List<T>>(
            stream, ExternalJsonOptions, effectiveToken);

        return result ?? [];
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
            if (_plutoCache.TryGetValue(
                    provider.PublicKey,
                    out var cached)
                && DateTime.UtcNow - cached.FetchedAt < PlutoCacheTtl)
            {
                return cached.Data;
            }

            using var httpClient =
                CreateHttpClient(provider);

            var raw =
                await httpClient.GetFromJsonAsync<List<PlutoChannel>>(
                    provider.ChannelsEndpoint,
                    cancellationToken)
                ?? [];

            _plutoCache[provider.PublicKey] =
                (DateTime.UtcNow, raw);

            return raw;
        }
        finally
        {
            _plutoCacheLock.Release();
        }
    }

    private async Task<List<ExternalChannel>>
        FetchPlutoChannelsAsync(
            HttpClient httpClient,
            IptvProviders provider,
            CancellationToken cancellationToken)
    {
        var raw =
            await FetchPlutoRawAsync(
                provider,
                cancellationToken);

        return raw
            .Where(c =>
                !string.IsNullOrWhiteSpace(c.Id)
                && !string.IsNullOrWhiteSpace(c.Name))
            .Select(c => new ExternalChannel
            {
                Id = c.Id,
                Name = c.Name,
                Logo = c.Logo?.Path,
                Categories =
                    string.IsNullOrWhiteSpace(c.Category)
                        ? []
                        : [c.Category],
                Country = "US"
            })
            .ToList();
    }

    private async Task<List<ExternalStream>>
        FetchPlutoStreamsAsync(
            IptvProviders provider,
            CancellationToken cancellationToken)
    {
        var raw =
            await FetchPlutoRawAsync(
                provider,
                cancellationToken);

        using var httpClient =
            CreateHttpClient(provider);

        var streams = new List<ExternalStream>();

        foreach (var channel in raw)
        {
            if (string.IsNullOrWhiteSpace(channel.Id))
                continue;

            var hlsUrl =
                channel.Stitched?.Urls?
                    .FirstOrDefault(u => string.Equals(
                        u.Type,
                        "hls",
                        StringComparison.OrdinalIgnoreCase))
                    ?.Url
                ?? channel.Stitched?.Urls?
                    .FirstOrDefault()?.Url;

            if (string.IsNullOrWhiteSpace(hlsUrl))
                continue;

            try
            {
                var variants =
                    await ExtractHlsVariantsAsync(
                        httpClient,
                        hlsUrl,
                        cancellationToken);

                if (variants.Count > 0)
                {
                    foreach (var variant in variants)
                    {
                        streams.Add(new ExternalStream
                        {
                            Channel = channel.Id,
                            Url = variant.Url,
                            Quality = variant.Quality
                        });
                    }

                    continue;
                }
            }
            catch
            {
                // If the master playlist cannot be read,
                // keep the original HLS stream as Auto.
            }

            streams.Add(new ExternalStream
            {
                Channel = channel.Id,
                Url = hlsUrl,
                Quality = "Auto"
            });
        }

        return streams;
    }

    private static async Task<List<HlsVariant>>
        ExtractHlsVariantsAsync(
            HttpClient httpClient,
            string masterUrl,
            CancellationToken cancellationToken)
    {
        using var response =
            await httpClient.GetAsync(
                masterUrl,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var playlist =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (string.IsNullOrWhiteSpace(playlist))
            return [];

        var lines =
            playlist
                .Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries);

        var variants = new List<HlsVariant>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (!line.StartsWith(
                    "#EXT-X-STREAM-INF:",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var attributes =
                line["#EXT-X-STREAM-INF:".Length..];

            var resolution =
                GetHlsAttribute(
                    attributes,
                    "RESOLUTION");

            var bandwidth =
                GetHlsAttribute(
                    attributes,
                    "BANDWIDTH");

            string? variantUrl = null;

            for (var j = i + 1; j < lines.Length; j++)
            {
                var next = lines[j].Trim();

                if (string.IsNullOrWhiteSpace(next))
                    continue;

                if (next.StartsWith("#"))
                    continue;

                variantUrl = next;

                i = j;
                break;
            }

            if (string.IsNullOrWhiteSpace(variantUrl))
                continue;

            var absoluteUrl =
                ResolveHlsUrl(
                    masterUrl,
                    variantUrl);

            var quality =
                ResolveQuality(
                    resolution,
                    bandwidth);

            variants.Add(new HlsVariant
            {
                Url = absoluteUrl,
                Quality = quality
            });
        }

        return variants
            .GroupBy(v =>
                    $"{v.Quality}|{v.Url}",
                StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(v =>
                GetQualityRank(v.Quality))
            .ToList();
    }

    private static string? GetHlsAttribute(
        string attributes,
        string attributeName)
    {
        var parts =
            attributes.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var separator =
                part.IndexOf('=');

            if (separator <= 0)
                continue;

            var key =
                part[..separator].Trim();

            if (!string.Equals(
                    key,
                    attributeName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return part[(separator + 1)..]
                .Trim()
                .Trim('"');
        }

        return null;
    }

    private static string ResolveQuality(
        string? resolution,
        string? bandwidth)
    {
        if (!string.IsNullOrWhiteSpace(resolution))
        {
            var xIndex =
                resolution.IndexOf(
                    'x',
                    StringComparison.OrdinalIgnoreCase);

            if (xIndex > 0
                && int.TryParse(
                    resolution[(xIndex + 1)..],
                    out var height))
            {
                return $"{height}p";
            }
        }

        // Some playlists don't expose RESOLUTION.
        // Use bandwidth only as a fallback.
        if (long.TryParse(
                bandwidth,
                out var bitrate))
        {
            return bitrate switch
            {
                >= 8_000_000 => "1080p",
                >= 4_000_000 => "720p",
                >= 2_000_000 => "480p",
                >= 1_000_000 => "360p",
                _ => "Auto"
            };
        }

        return "Auto";
    }

    private static int GetQualityRank(
        string quality)
    {
        return quality.ToLowerInvariant() switch
        {
            "2160p" => 2160,
            "1440p" => 1440,
            "1080p" => 1080,
            "720p" => 720,
            "576p" => 576,
            "480p" => 480,
            "360p" => 360,
            "240p" => 240,
            _ => 0
        };
    }

    private static string ResolveHlsUrl(
        string masterUrl,
        string variantUrl)
    {
        if (Uri.TryCreate(
                variantUrl,
                UriKind.Absolute,
                out var absolute))
        {
            return absolute.ToString();
        }

        if (!Uri.TryCreate(
                masterUrl,
                UriKind.Absolute,
                out var master))
        {
            return variantUrl;
        }

        return new Uri(
            master,
            variantUrl).ToString();
    }

    private sealed class HlsVariant
    {
        public string Url { get; set; } = string.Empty;

        public string Quality { get; set; } = "Auto";
    }

    #endregion
}