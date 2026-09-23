using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._Channel.Constants;
using iptv.Services._Channel.Contracts;
using iptv.Services._Channel.DTOs.Results;
using iptv.Services._Channel.DTOs.Updates;
using iptv.Services._IptvNotifier.Contracts;
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
    IStreamRepository _streamRepository)
    : IChannelService, RegisterMode.IScopedDependency
{
     private static readonly SemaphoreSlim _liteDataCacheLock = new(1, 1);
 
    private static (DateTime FetchedAt, List<ChannelLiteProjection> Channels, List<StreamLiteProjection> Streams)
        _liteDataCache;
    
    private static readonly TimeSpan LiteDataCacheTtl = TimeSpan.FromSeconds(30);
 
    public async Task<List<ChannelWithStreamResult>> GetCuratedListWithStreamAsync(
        CancellationToken cancellationToken = default)
    {
        var (channels, streams) = await GetCachedLiteDataAsync(cancellationToken);
 
        if (channels.Count == 0)
            return [];
 
        var streamsByChannel = streams.ToLookup(q => q.ChannelId);
 
        var whitelisted = channels
            .Where(c => CuratedChannelWhitelist.Contains(c.Name))
            .GroupBy(c => ChannelNameNormalizer.Normalize(c.Name));
 
        var result = new List<ChannelWithStreamResult>();
 
        foreach (var group in whitelisted)
        {
            ChannelLiteProjection winnerChannel = null;
            StreamLiteProjection winnerStream = null;
 
            foreach (var channel in group)
            {
                var candidate = SelectBestStream(streamsByChannel[channel.ChannelId], channel.CurrentStreamId);
                if (candidate == null) continue;
 
                if (winnerStream == null || candidate.QualityRank > winnerStream.QualityRank)
                {
                    winnerStream = candidate;
                    winnerChannel = channel;
                }
            }
 
            winnerChannel ??= group.First();
 
            result.Add(MapToChannelWithStreamResult(winnerChannel, winnerStream));
        }
 
        return result;
    }
 
    public async Task<List<AllChannelWithStreamResult>> GetAllUnpagedWithStreamAsync(
        CancellationToken cancellationToken = default)
    {
        var (channels, streams) = await GetCachedLiteDataAsync(cancellationToken);
 
        if (channels.Count == 0)
            return [];
 
        var streamsByChannel = streams.ToLookup(q => q.ChannelId);
 
        var result = new List<AllChannelWithStreamResult>(channels.Count);
 
        foreach (var channel in channels)
        {
            var best = SelectBestStream(streamsByChannel[channel.ChannelId], channel.CurrentStreamId);
            result.Add(MapToAllChannelWithStreamResult(channel, best));
        }
 
        return result;
    }
    
    public async Task<MonjoFilteredResult<ChannelBasicResult>> GetAllAsync(
        MonjoQuery query)
    {
        query.WithBase<ChannelBasicResult>();
 
        return await _channelRepository
            .AsQueryable()
            .Where(q => !q.Inactive)
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
        MonjoQuery query,
        GetChannelsFilteredUpdate update)
    {
        query.WithBase<ChannelFilteredResult>();
 
        if (string.IsNullOrEmpty(update.Country) &&
            string.IsNullOrEmpty(update.Category) &&
            string.IsNullOrEmpty(update.ImageUri))
            throw new BadRequestException("Please select at least one filter criteria.");
 
        var queryResult = _channelRepository.AsQueryable()
            .Where(q => !q.Inactive &&
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
 
    public async Task<MonjoFilteredResult<ChannelFilteredResult>> GetAllForAdminAsync(
        MonjoQuery query)
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
        var query = _channelRepository.AsQueryable().Where(q => !q.Inactive);
 
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
 
        return (new ChannelBasicResult
        {
            ChannelId = channel.ChannelId,
            Name = channel.Name,
            ImageUri = channel.ImageUri,
            Country = channel.Country,
            Category = channel.Category
        });
    }
 
    public async Task<ChannelFilteredResult> ActivateAsync(ChannelActivateUpdate update)
    {
        var channel = await _channelRepository.GetByChannelIdAsync(update.ChannelId)
                      ?? throw new NotFoundException("Channel not found.");
 
        var newUpdate = Builders<Channels>.Update
            .Set(q => q.Inactive, !update.ShouldActivate)
            .Set(q => q.ModifiedMoment, DateTime.UtcNow);
 
        await _channelRepository.FindOneAndUpdateAsync(
            q => q.Id == channel.Id, newUpdate);
 
        channel.Inactive = !update.ShouldActivate;
 
        InvalidateLiteDataCache();
 
        await _eventPublisher.PublishChannelChangedAsync(channel,
            update.ShouldActivate ? "Activated" : "Deactivated");
 
        return MapToResult(channel);
    }
 
    public async Task<string> DeleteAsync(ChannelDeleteUpdate update)
    {
        var channel = await _channelRepository.GetByChannelIdAsync(update.ChannelId)
                      ?? throw new NotFoundException("Channel not found.");
 
        await _channelRepository.DeleteOneAsync(q => q.Id == channel.Id);
 
        InvalidateLiteDataCache();
 
        await _eventPublisher.PublishChannelChangedAsync(channel, "Deleted");
 
        return channel.ChannelId;
    }
    
    #region Helpers - Shared Fetch, Cache & Selection
 
    private async Task<(List<ChannelLiteProjection> Channels, List<StreamLiteProjection> Streams)>
        GetCachedLiteDataAsync(CancellationToken cancellationToken)
    {
        if (_liteDataCache.Channels != null &&
            DateTime.UtcNow - _liteDataCache.FetchedAt < LiteDataCacheTtl)
        {
            return (_liteDataCache.Channels, _liteDataCache.Streams);
        }
 
        await _liteDataCacheLock.WaitAsync(cancellationToken);
 
        try
        {
            // Someone else may have already refreshed the cache while we
            // were waiting for the lock - re-check before hitting Mongo.
            if (_liteDataCache.Channels != null &&
                DateTime.UtcNow - _liteDataCache.FetchedAt < LiteDataCacheTtl)
            {
                return (_liteDataCache.Channels, _liteDataCache.Streams);
            }
 
            var fresh = await FetchLiteChannelsAndStreamsAsync(cancellationToken);
 
            _liteDataCache = (DateTime.UtcNow, fresh.Channels, fresh.Streams);
 
            return fresh;
        }
        finally
        {
            _liteDataCacheLock.Release();
        }
    }
 
    private static void InvalidateLiteDataCache()
    {
        _liteDataCache = default;
    }

    // Passthrough so admin operations in other services (e.g. provider cascade) can invalidate the
    // shared lite cache without changing its caching mechanics.
    internal static void InvalidateSharedLiteCache() => InvalidateLiteDataCache();
 
    private async Task<(List<ChannelLiteProjection> Channels, List<StreamLiteProjection> Streams)>
        FetchLiteChannelsAndStreamsAsync(CancellationToken cancellationToken)
    {
        var channelsTask = await _channelRepository
            .AsQueryable()
            .Where(q => !q.Inactive)
            .OrderBy(q => q.Name)
            .Select(q => new ChannelLiteProjection
            {
                ChannelId = q.ChannelId,
                Name = q.Name,
                ImageUri = q.ImageUri,
                Country = q.Country,
                Category = q.Category,
                CurrentStreamId = q.CurrentStreamId
            })
            .ToListAsync(cancellationToken);
 
        var streamsTask = await _streamRepository
            .AsQueryable()
            .Where(q => !q.Inactive && q.IsHealthy)
            .Select(q => new StreamLiteProjection
            {
                StreamId = q.StreamId,
                ChannelId = q.ChannelId,
                StreamUri = q.StreamUri,
                UserAgent = q.UserAgent,
                Referer = q.Referer,
                Quality = q.Quality,
                QualityRank = q.QualityRank
            })
            .ToListAsync(cancellationToken);
 
        return (channelsTask, streamsTask);
    }
 
    private static StreamLiteProjection SelectBestStream(
        IEnumerable<StreamLiteProjection> candidates,
        string currentStreamId)
    {
        StreamLiteProjection currentMatch = null;
        StreamLiteProjection bestByQuality = null;
 
        foreach (var stream in candidates)
        {
            if (currentMatch == null &&
                !string.IsNullOrEmpty(currentStreamId) &&
                stream.StreamId == currentStreamId)
                currentMatch = stream;
 
            if (bestByQuality == null || stream.QualityRank > bestByQuality.QualityRank)
                bestByQuality = stream;
        }
 
        return currentMatch ?? bestByQuality;
    }
 
    private static ChannelWithStreamResult MapToChannelWithStreamResult(
        ChannelLiteProjection channel, StreamLiteProjection stream)
        => new()
        {
            ChannelId = channel.ChannelId,
            Name = channel.Name,
            ImageUri = channel.ImageUri,
            Country = channel.Country,
            Category = channel.Category,
            CurrentStreamUrl = stream?.StreamUri
        };
 
    private static AllChannelWithStreamResult MapToAllChannelWithStreamResult(
        ChannelLiteProjection channel, StreamLiteProjection stream)
        => new()
        {
            ChannelId = channel.ChannelId,
            Name = channel.Name,
            ImageUri = channel.ImageUri,
            Country = channel.Country,
            Category = channel.Category,
            CurrentStreamUrl = stream?.StreamUri,
            StreamUserAgent = stream?.UserAgent,
            StreamReferer = stream?.Referer,
            StreamQuality = stream?.Quality
        };
 
    private sealed class ChannelLiteProjection
    {
        public string ChannelId { get; set; }
        public string Name { get; set; }
        public string ImageUri { get; set; }
        public string Country { get; set; }
        public string Category { get; set; }
        public string CurrentStreamId { get; set; }
    }
 
    private sealed class StreamLiteProjection
    {
        public string StreamId { get; set; }
        public string ChannelId { get; set; }
        public string StreamUri { get; set; }
        public string UserAgent { get; set; }
        public string Referer { get; set; }
        public string Quality { get; set; }
        public int QualityRank { get; set; }
    }
 
    #endregion
 
    #region Helpers
 
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