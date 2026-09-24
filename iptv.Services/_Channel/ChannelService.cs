using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._Channel.Contracts;
using iptv.Services._Channel.DTOs.Results;
using iptv.Services._Channel.DTOs.Updates;
using iptv.Services._ChannelRegistry;
using iptv.Services._ChannelRegistry.Contracts;
using iptv.Services._Canonical;
using iptv.Services._IptvNotifier.Contracts;
using iptv.Services._Stream.Selection;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Utilities.Constants;
using Utilities.Exceptions.Common;
using Utilities.Models.Updates;
using Utilities.MongoDatabase.Extensions;
using Utilities.MongoDatabase.Filter;
using Utilities.Utilities;

namespace iptv.Services._Channel;

public class ChannelService(
    IChannelRepository _channelRepository,
    IIptvEventPublisher _eventPublisher,
    IStreamRepository _streamRepository,
    IIptvProviderRepository _providerRepository,
    IChannelRegistryService _registryService,
    IStreamSelector _streamSelector)
    : IChannelService, RegisterMode.IScopedDependency
{
    private static readonly SemaphoreSlim _liteDataCacheLock = new(1, 1);

    private static LiteData _liteDataCache;

    private static readonly TimeSpan LiteDataCacheTtl = TimeSpan.FromSeconds(30);

    #region Canonical list endpoints

    public async Task<List<AllChannelWithStreamResult>> GetAllUnpagedWithStreamAsync(
        CancellationToken cancellationToken = default)
    {
        var lite = await GetCachedLiteDataAsync(cancellationToken);
        if (lite.Channels.Count == 0)
            return [];

        var index = await _registryService.GetIndexAsync(cancellationToken);
        var streamsByChannel = lite.Streams.ToLookup(s => s.ChannelId);
        var now = DateTime.UtcNow;

        var result = new List<AllChannelWithStreamResult>();

        foreach (var group in lite.Channels.GroupBy(c => CanonicalKey(c)))
        {
            var selection = SelectForCanonical(group, streamsByChannel, lite.Providers, now);
            if (selection.Winner == null)
                continue; // never return a canonical channel with no playable stream

            var display = PickDisplayChannel(group, lite.Providers);
            index.ByCanonicalId.TryGetValue(group.Key, out var reg);

            result.Add(new AllChannelWithStreamResult
            {
                ChannelId = display.ChannelId,
                CanonicalId = group.Key,
                Name = reg?.Name ?? display.Name,
                NameFa = reg?.NameFa,
                CuratedCountry = reg?.CuratedCountry,
                ImageUri = display.ImageUri,
                Country = ResolveDisplayCountry(display.Country, reg),
                Category = reg?.Categories?.FirstOrDefault() ?? display.Category,
                CurrentStreamUrl = selection.Winner.StreamUri,
                StreamId = selection.Winner.StreamId,
                StreamUserAgent = selection.Winner.UserAgent,
                StreamReferer = selection.Winner.Referer,
                StreamQuality = selection.Winner.Quality,
                ProviderName = selection.Winner.ProviderName,
                FallbackStreams = selection.Fallbacks
            });
        }

        return result.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<List<ChannelWithStreamResult>> GetCuratedListWithStreamAsync(
        string country = null, CancellationToken cancellationToken = default)
    {
        var lite = await GetCachedLiteDataAsync(cancellationToken);
        var index = await _registryService.GetIndexAsync(cancellationToken);
        if (lite.Channels.Count == 0 || index.ByCanonicalId.Count == 0)
            return [];

        var streamsByChannel = lite.Streams.ToLookup(s => s.ChannelId);
        var channelsByCanonical = lite.Channels.ToLookup(CanonicalKey);
        var now = DateTime.UtcNow;

        var wantedCountry = string.IsNullOrWhiteSpace(country) ? null : country.Trim();

        var entries = index.ByCanonicalId.Values
            .Where(e => !e.Inactive)
            .Where(e => wantedCountry == null ||
                        string.Equals(e.CuratedCountry, wantedCountry, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => CuratedCountries.OrderKey(e.CuratedCountry))
            .ThenBy(e => e.CuratedRank == 0 ? int.MaxValue : e.CuratedRank)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase);

        var result = new List<ChannelWithStreamResult>();

        foreach (var entry in entries)
        {
            var group = channelsByCanonical[entry.CanonicalId].ToList();
            if (group.Count == 0)
                continue;

            var selection = SelectForCanonical(group, streamsByChannel, lite.Providers, now);
            if (selection.Winner == null)
                continue;

            var display = PickDisplayChannel(group, lite.Providers);

            result.Add(new ChannelWithStreamResult
            {
                ChannelId = display.ChannelId,
                CanonicalId = entry.CanonicalId,
                Name = entry.Name ?? display.Name,
                NameFa = entry.NameFa,
                CuratedCountry = entry.CuratedCountry,
                ImageUri = display.ImageUri,
                Country = ResolveDisplayCountry(display.Country, entry),
                Category = entry.Categories?.FirstOrDefault() ?? display.Category,
                CurrentStreamUrl = selection.Winner.StreamUri,
                StreamId = selection.Winner.StreamId,
                UserAgent = selection.Winner.UserAgent,
                Referer = selection.Winner.Referer,
                Quality = selection.Winner.Quality,
                FallbackStreams = selection.Fallbacks,
                Inactive = false
            });
        }

        return result;
    }

    public async Task<List<CuratedCountryResult>> GetCuratedCountriesAsync(
        CancellationToken cancellationToken = default)
    {
        var lite = await GetCachedLiteDataAsync(cancellationToken);
        var index = await _registryService.GetIndexAsync(cancellationToken);

        var streamsByChannel = lite.Streams.ToLookup(s => s.ChannelId);
        var channelsByCanonical = lite.Channels.ToLookup(CanonicalKey);
        var now = DateTime.UtcNow;

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in index.ByCanonicalId.Values.Where(e => !e.Inactive))
        {
            var group = channelsByCanonical[entry.CanonicalId].ToList();
            if (group.Count == 0)
                continue;

            var selection = SelectForCanonical(group, streamsByChannel, lite.Providers, now);
            if (selection.Winner == null)
                continue;

            var key = entry.CuratedCountry ?? "intl";
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts
            .Select(kv => new CuratedCountryResult
            {
                Key = kv.Key,
                DisplayEn = CuratedCountries.Info(kv.Key).DisplayEn,
                DisplayFa = CuratedCountries.Info(kv.Key).DisplayFa,
                Count = kv.Value
            })
            .OrderBy(c => CuratedCountries.OrderKey(c.Key))
            .ToList();
    }

    #endregion

    #region Selection helpers

    private (StreamCandidate Winner, List<FallbackStreamResult> Fallbacks) SelectForCanonical(
        IEnumerable<ChannelLiteProjection> group,
        ILookup<string, StreamLiteProjection> streamsByChannel,
        Dictionary<string, ProviderLite> providers,
        DateTime now)
    {
        var candidates = new List<StreamCandidate>();
        string currentStreamId = null;

        foreach (var channel in group)
        {
            if (currentStreamId == null && !string.IsNullOrEmpty(channel.CurrentStreamId))
                currentStreamId = channel.CurrentStreamId;

            foreach (var s in streamsByChannel[channel.ChannelId])
            {
                var provider = providers.GetValueOrDefault(s.ProviderPublicKey);
                candidates.Add(new StreamCandidate
                {
                    StreamId = s.StreamId,
                    ChannelId = s.ChannelId,
                    CanonicalId = channel.CanonicalId,
                    ProviderPublicKey = s.ProviderPublicKey,
                    ProviderName = provider?.Name,
                    ProviderPriority = provider?.Priority ?? 0,
                    StreamUri = s.StreamUri,
                    UserAgent = string.IsNullOrWhiteSpace(s.UserAgent) ? null : s.UserAgent,
                    Referer = string.IsNullOrWhiteSpace(s.Referer) ? null : s.Referer,
                    Quality = s.Quality,
                    QualityRank = s.QualityRank,
                    IsAdaptive = s.IsAdaptive,
                    IsHealthy = s.IsHealthy,
                    ServerProbeUnreliable = s.ServerProbeUnreliable,
                    RecentReportCount = StreamCandidateFactory.RecentReportCount(s.RecentClientFailures, now),
                    ClientFailingUntil = s.ClientFailingUntil
                });
            }
        }

        var ordered = _streamSelector.Order(candidates,
            new StreamSelectionContext { CurrentStreamId = currentStreamId, Now = now });

        if (ordered.Count == 0)
            return (null, []);

        var fallbacks = ordered.Skip(1).Take(4).Select(c => new FallbackStreamResult
        {
            StreamId = c.StreamId,
            Url = c.StreamUri,
            UserAgent = c.UserAgent,
            Referer = c.Referer,
            Quality = c.Quality,
            ProviderName = c.ProviderName
        }).ToList();

        return (ordered[0], fallbacks);
    }

    private static ChannelLiteProjection PickDisplayChannel(
        IEnumerable<ChannelLiteProjection> group, Dictionary<string, ProviderLite> providers)
        => group
            .OrderByDescending(c => providers.GetValueOrDefault(c.ProviderPublicKey)?.Priority ?? 0)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .First();

    private static string CanonicalKey(ChannelLiteProjection c)
        => string.IsNullOrWhiteSpace(c.CanonicalId) ? c.ChannelId : c.CanonicalId;

    /// <summary>
    /// The Flutter app groups channels by the ISO <c>country</c> field, so it must never be empty
    /// (M3U sources such as shayanline carry no tvg-country). Persian channels (iran / iran-foreign)
    /// are reported as IR so they are grouped together for Iranian users; other curated keys map to
    /// their ISO code; iptv-org's non-standard "UK" becomes "GB".
    /// </summary>
    private static string ResolveDisplayCountry(string channelCountry, ChannelRegistry reg)
    {
        if (reg != null)
        {
            if (string.Equals(reg.CuratedCountry, "iran", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reg.CuratedCountry, "iran-foreign", StringComparison.OrdinalIgnoreCase))
                return "IR";

            if (reg.CuratedCountry?.Length == 2)
                return NormalizeIso(reg.CuratedCountry);

            if (!string.IsNullOrWhiteSpace(reg.SourceCountry))
                return NormalizeIso(reg.SourceCountry);
        }

        return NormalizeIso(channelCountry);
    }

    private static string NormalizeIso(string country)
    {
        var c = (country ?? string.Empty).Trim().ToUpperInvariant();
        return c == "UK" ? "GB" : c;
    }

    #endregion

    #region Admin & basic queries

    public async Task<MonjoFilteredResult<ChannelBasicResult>> GetAllAsync(MonjoQuery query)
    {
        query.WithBase<ChannelBasicResult>();

        return await _channelRepository
            .AsQueryable()
            .Where(q => !q.Inactive && !q.AdminDisabled)
            .Apply(query.Where, nameof(ChannelBasicResult))
            .Apply(query.Order, nameof(ChannelBasicResult))
            .Select(n => new ChannelBasicResult
            {
                ChannelId = n.ChannelId,
                Name = n.Name,
                ImageUri = n.ImageUri,
                Country = n.Country,
                Category = n.Category,
                CurrentStreamId = n.CurrentStreamId
            })
            .ExecuteAsync(query, nameof(ChannelBasicResult));
    }

    public async Task<MonjoFilteredResult<ChannelBasicResult>> GetChannelsFilteredAsync(
        MonjoQuery query, GetChannelsFilteredUpdate update)
    {
        query.WithBase<ChannelFilteredResult>();

        if (string.IsNullOrEmpty(update.Country) &&
            string.IsNullOrEmpty(update.Category) &&
            string.IsNullOrEmpty(update.ImageUri))
            throw new BadRequestException("Please select at least one filter criteria.");

        var queryResult = _channelRepository.AsQueryable()
            .Where(q => !q.Inactive && !q.AdminDisabled &&
                        (string.IsNullOrEmpty(update.Country) || q.Country.ToLower() == update.Country.ToLower()) &&
                        (string.IsNullOrEmpty(update.Category) || q.Category.ToLower() == update.Category.ToLower()) &&
                        (string.IsNullOrEmpty(update.ImageUri) ||
                         q.ImageUri.ToLower().Contains(update.ImageUri.ToLower())))
            .Apply(query.Where, nameof(ChannelFilteredResult))
            .Apply(query.Order, nameof(ChannelFilteredResult))
            .Select(n => new ChannelBasicResult
            {
                ChannelId = n.ChannelId,
                Name = n.Name,
                ImageUri = n.ImageUri,
                Country = n.Country,
                Category = n.Category,
                CurrentStreamId = n.CurrentStreamId
            });

        var channel = await queryResult.ExecuteAsync(query, nameof(ChannelFilteredResult));

        if (channel.Data == null || channel.Data.Count == 0)
            throw new NotFoundException("Channel not found for the specified criteria.");

        return channel;
    }

    public async Task<MonjoFilteredResult<ChannelFilteredResult>> GetAllForAdminAsync(MonjoQuery query)
    {
        query.WithBase<ChannelFilteredResult>();

        return await _channelRepository
            .AsQueryable()
            .Apply(query.Where, nameof(ChannelFilteredResult))
            .Apply(query.Order, nameof(ChannelFilteredResult))
            .Select(MapToResult())
            .ExecuteAsync(query, nameof(ChannelFilteredResult));
    }

    public async Task<ManualPaginationResult<ChannelResult>> GetChannelWithSearchAsync(GetAllChannelUpdate update)
    {
        var query = _channelRepository.AsQueryable().Where(q => !q.Inactive && !q.AdminDisabled);

        if (!string.IsNullOrEmpty(update.Search))
        {
            query = query.Where(channels => channels.Category.ToLower().Contains(update.Search.ToLower()) ||
                                            channels.Country.ToLower() == update.Search.ToLower() ||
                                            channels.Name.ToLower().Contains(update.Search.ToLower())
            );
        }

        if (update.OrderBy)
            query = query.OrderBy(channels => channels.Name);
        else
            query = query.OrderByDescending(channels => channels.Name);

        var mappedQuery = query.Select(channels => new
        {
            channels.ChannelId,
            channels.Name,
            channels.ImageUri,
            channels.Country,
            channels.Category
        });

        var paginatedArticles = await mappedQuery.PaginateAsync(update.Page, update.Size);

        ManualPaginationResult<ChannelResult> result = new()
        {
            PageCount = paginatedArticles.PageCount,
            TotalCount = paginatedArticles.TotalCount,
            Data =
            [
                .. paginatedArticles.Data.Select(channel => new ChannelResult()
                {
                    ChannelId = channel.ChannelId,
                    Name = channel.Name,
                    ImageUri = channel.ImageUri,
                    Country = channel.Country,
                    Category = channel.Category
                })
            ],
        };

        return result;
    }

    public async Task<ChannelBasicResult> GetByChannelIdAsync(GetGlobalIdUpdate channelId)
    {
        var channel = await _channelRepository.GetByChannelIdAsync(channelId.Id)
                      ?? throw new NotFoundException("Channel not found.");

        return new ChannelBasicResult
        {
            ChannelId = channel.ChannelId,
            Name = channel.Name,
            ImageUri = channel.ImageUri,
            Country = channel.Country,
            Category = channel.Category
        };
    }

    public async Task<ChannelFilteredResult> ActivateAsync(ChannelActivateUpdate update)
    {
        var channel = await _channelRepository.GetByChannelIdAsync(update.ChannelId)
                      ?? throw new NotFoundException("Channel not found.");

        // Admin intent lives in AdminDisabled so a later sync cannot silently re-enable it.
        var newUpdate = Builders<Channels>.Update
            .Set(q => q.AdminDisabled, !update.ShouldActivate)
            .Set(q => q.ModifiedMoment, DateTime.UtcNow);

        await _channelRepository.FindOneAndUpdateAsync(q => q.Id == channel.Id, newUpdate);

        channel.AdminDisabled = !update.ShouldActivate;

        InvalidateLiteDataCache();

        await _eventPublisher.PublishChannelChangedAsync(channel,
            update.ShouldActivate ? "Activated" : "Deactivated");

        return MapToResult(channel);
    }

    public async Task<string> DeleteAsync(ChannelDeleteUpdate update)
    {
        var channel = await _channelRepository.GetByChannelIdAsync(update.ChannelId)
                      ?? throw new NotFoundException("Channel not found.");

        // Deleting a channel also removes its streams.
        await _streamRepository.DeleteManyAsync(q => q.ChannelId == channel.ChannelId);
        await _channelRepository.DeleteOneAsync(q => q.Id == channel.Id);

        InvalidateLiteDataCache();

        await _eventPublisher.PublishChannelChangedAsync(channel, "Deleted");

        return channel.ChannelId;
    }

    #endregion

    #region Lite cache (mechanics unchanged — TTL, lock, double-check)

    private async Task<LiteData> GetCachedLiteDataAsync(CancellationToken cancellationToken)
    {
        if (_liteDataCache != null &&
            DateTime.UtcNow - _liteDataCache.FetchedAt < LiteDataCacheTtl)
            return _liteDataCache;

        await _liteDataCacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_liteDataCache != null &&
                DateTime.UtcNow - _liteDataCache.FetchedAt < LiteDataCacheTtl)
                return _liteDataCache;

            var fresh = await FetchLiteDataAsync(cancellationToken);
            _liteDataCache = fresh;
            return fresh;
        }
        finally
        {
            _liteDataCacheLock.Release();
        }
    }

    private static void InvalidateLiteDataCache() => _liteDataCache = null;

    internal static void InvalidateSharedLiteCache() => InvalidateLiteDataCache();

    private async Task<LiteData> FetchLiteDataAsync(CancellationToken cancellationToken)
    {
        var channels = await _channelRepository
            .AsQueryable()
            .Where(q => !q.Inactive && !q.AdminDisabled)
            .OrderBy(q => q.Name)
            .Select(q => new ChannelLiteProjection
            {
                ChannelId = q.ChannelId,
                CanonicalId = q.CanonicalId,
                Name = q.Name,
                ImageUri = q.ImageUri,
                Country = q.Country,
                Category = q.Category,
                CurrentStreamId = q.CurrentStreamId,
                ProviderPublicKey = q.ProviderPublicKey
            })
            .ToListAsync(cancellationToken);

        var streams = await _streamRepository
            .AsQueryable()
            .Where(q => !q.Inactive && !q.AdminDisabled)
            .Select(q => new StreamLiteProjection
            {
                StreamId = q.StreamId,
                ChannelId = q.ChannelId,
                ProviderPublicKey = q.ProviderPublicKey,
                StreamUri = q.StreamUri,
                UserAgent = q.UserAgent,
                Referer = q.Referer,
                Quality = q.Quality,
                QualityRank = q.QualityRank,
                IsAdaptive = q.IsAdaptive,
                IsHealthy = q.IsHealthy,
                ServerProbeUnreliable = q.ServerProbeUnreliable,
                ClientFailingUntil = q.ClientFailingUntil,
                RecentClientFailures = q.RecentClientFailures
            })
            .ToListAsync(cancellationToken);

        var providers = (await _providerRepository
                .AsQueryable()
                .Where(q => !q.Inactive)
                .Select(q => new ProviderLite
                {
                    PublicKey = q.PublicKey,
                    Name = q.Name,
                    Priority = q.Priority
                })
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.PublicKey)
            .ToDictionary(g => g.Key, g => g.First());

        return new LiteData
        {
            FetchedAt = DateTime.UtcNow,
            Channels = channels,
            Streams = streams,
            Providers = providers
        };
    }

    private sealed class LiteData
    {
        public DateTime FetchedAt { get; init; }
        public List<ChannelLiteProjection> Channels { get; init; } = [];
        public List<StreamLiteProjection> Streams { get; init; } = [];
        public Dictionary<string, ProviderLite> Providers { get; init; } = [];
    }

    private sealed class ChannelLiteProjection
    {
        public string ChannelId { get; set; }
        public string CanonicalId { get; set; }
        public string Name { get; set; }
        public string ImageUri { get; set; }
        public string Country { get; set; }
        public string Category { get; set; }
        public string CurrentStreamId { get; set; }
        public string ProviderPublicKey { get; set; }
    }

    private sealed class StreamLiteProjection
    {
        public string StreamId { get; set; }
        public string ChannelId { get; set; }
        public string ProviderPublicKey { get; set; }
        public string StreamUri { get; set; }
        public string UserAgent { get; set; }
        public string Referer { get; set; }
        public string Quality { get; set; }
        public int QualityRank { get; set; }
        public bool IsAdaptive { get; set; }
        public bool IsHealthy { get; set; }
        public bool ServerProbeUnreliable { get; set; }
        public DateTime? ClientFailingUntil { get; set; }
        public List<ClientFailureReport> RecentClientFailures { get; set; }
    }

    private sealed class ProviderLite
    {
        public string PublicKey { get; set; }
        public string Name { get; set; }
        public int Priority { get; set; }
    }

    #endregion

    #region Mappers

    private static System.Linq.Expressions.Expression<Func<Channels, ChannelFilteredResult>> MapToResult()
        => channel => new ChannelFilteredResult
        {
            ChannelId = channel.ChannelId,
            ProviderPublicKey = channel.ProviderPublicKey,
            ExternalId = channel.ExternalId,
            Name = channel.Name,
            ImageUri = channel.ImageUri,
            Country = channel.Country,
            Category = channel.Category,
            CurrentStreamId = channel.CurrentStreamId,
            Inactive = channel.Inactive,
            CreatedMoment = channel.CreatedMoment,
            ModifiedMoment = channel.ModifiedMoment
        };

    private static ChannelFilteredResult MapToResult(Channels channel)
        => new()
        {
            ChannelId = channel.ChannelId,
            ProviderPublicKey = channel.ProviderPublicKey,
            ExternalId = channel.ExternalId,
            Name = channel.Name,
            ImageUri = channel.ImageUri,
            Country = channel.Country,
            Category = channel.Category,
            CurrentStreamId = channel.CurrentStreamId,
            Inactive = channel.Inactive,
            CreatedMoment = channel.CreatedMoment,
            ModifiedMoment = channel.ModifiedMoment
        };

    #endregion
}
