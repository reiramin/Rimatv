using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
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
    public async Task<MonjoFilteredResult<ChannelWithStreamResult>> GetAllWithStreamAsync(
        MonjoQuery query)
    {
        query.WithBase<ChannelWithStreamResult>();

        var channelsQuery = _channelRepository
            .AsQueryable()
            .Where(q => !q.Inactive)
            .Apply(query.Where, nameof(ChannelWithStreamResult))
            .Apply(query.Order, nameof(ChannelWithStreamResult));

        var totalCount = await channelsQuery.CountAsync();

        if (totalCount == 0)
            return new MonjoFilteredResult<ChannelWithStreamResult>
            {
                TotalCount = 0,
                PageCount = 0,
                Data = []
            };

        var pagedChannels = await channelsQuery
            .Apply(query.Page)
            .Select(q => new
            {
                q.ChannelId,
                q.Name,
                q.ImageUri,
                q.Country,
                q.Category,
                q.CurrentStreamId
            })
            .ToListAsync();

        var channelIds = pagedChannels
            .Select(q => q.ChannelId)
            .ToHashSet();

        var allStreams = await _streamRepository
            .AsQueryable()
            .Where(q =>
                channelIds.Contains(q.ChannelId) &&
                !q.Inactive &&
                q.IsHealthy)
            .OrderByDescending(q => q.QualityRank)
            .Select(q => new
            {
                q.StreamId,
                q.ChannelId,
                q.StreamUri,
                q.UserAgent,
                q.Referer,
                q.Type,
                q.Quality,
                q.QualityRank
            })
            .ToListAsync();

        var bestStreamByChannelId = pagedChannels
            .ToDictionary(
                channel => channel.ChannelId,
                channel =>
                {
                    var streamsByChannel = allStreams.Where(s => s.ChannelId == channel.ChannelId).ToList();

                    if (!streamsByChannel.Any())
                        return null;

                    if (!string.IsNullOrEmpty(channel.CurrentStreamId))
                    {
                        var current = streamsByChannel.FirstOrDefault(s =>
                            s.StreamId == channel.CurrentStreamId);

                        if (current != null)
                            return current;
                    }

                    return streamsByChannel
                        .OrderByDescending(s => s.QualityRank)
                        .FirstOrDefault();
                });

        var pageSize = query.Page?.Size ?? totalCount;
        var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

        var data = pagedChannels.Select(channel =>
        {
            bestStreamByChannelId.TryGetValue(channel.ChannelId, out var stream);

            return new ChannelWithStreamResult
            {
                ChannelId = channel.ChannelId,
                Name = channel.Name,
                ImageUri = channel.ImageUri,
                Country = channel.Country,
                Category = channel.Category,
                CurrentStreamId = stream == null
                    ? null
                    : stream.StreamId
            };
        }).ToList();

        return new MonjoFilteredResult<ChannelWithStreamResult>
        {
            TotalCount = totalCount,
            PageCount = pageCount,
            Data = data
        };
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

        await _eventPublisher.PublishChannelChangedAsync(channel,
            update.ShouldActivate ? "Activated" : "Deactivated");

        return MapToResult(channel);
    }

    public async Task<string> DeleteAsync(ChannelDeleteUpdate update)
    {
        var channel = await _channelRepository.GetByChannelIdAsync(update.ChannelId)
                      ?? throw new NotFoundException("Channel not found.");

        await _channelRepository.DeleteOneAsync(q => q.Id == channel.Id);

        await _eventPublisher.PublishChannelChangedAsync(channel, "Deleted");

        return channel.ChannelId;
    }

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