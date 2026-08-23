using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._IptvNotifier.Contracts;
using iptv.Services._Stream.Contracts;
using iptv.Services._Stream.DTOs.Results;
using iptv.Services._Stream.DTOs.Updates;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Utilities.Constants;
using Utilities.Exceptions.Common;
using Utilities.Models.Updates;

namespace iptv.Services._Stream;

public class StreamService(
    IStreamRepository _streamRepository,
    IChannelRepository _channelRepository,
    IIptvEventPublisher _eventPublisher)
    : IStreamService, RegisterMode.IScopedDependency
{
    public async Task<List<StreamFilteredResult>> GetByChannelAsync(GetGlobalIdUpdate channelId)
    {
        var channel = await _channelRepository.GetByChannelIdAsync(channelId.Id)
                      ?? throw new NotFoundException("Channel not found.");

        var streams = await _streamRepository
            .AsQueryable()
            .Where(q => q.ChannelId == channel.ChannelId && !q.Inactive)
            .OrderByDescending(q => q.IsHealthy)
            .ThenByDescending(q => q.QualityRank)
            .ToListAsync();

        return streams.Select(MapToResult).ToList();
    }

    public async Task<StreamPlaybackResult> GetPlaybackStreamAsync(GetGlobalIdUpdate channelId)
    {
        var channel = await _channelRepository.GetByChannelIdAsync(channelId.Id)
                      ?? throw new NotFoundException("Channel not found.");
        
        var selected = await _streamRepository
            .AsQueryable()
            .Where(q => q.ChannelId == channel.ChannelId &&
                        !q.Inactive &&
                        q.IsHealthy &&
                        (channel.CurrentStreamId == null || q.StreamId == channel.CurrentStreamId))
            .OrderByDescending(q => q.QualityRank)
            .FirstOrDefaultAsync();

        selected ??= await _streamRepository
            .AsQueryable()
            .Where(q => q.ChannelId == channel.ChannelId && !q.Inactive && q.IsHealthy)
            .OrderByDescending(q => q.QualityRank)
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException(
                "No healthy stream is currently available for this channel.");

        if (channel.CurrentStreamId != selected.StreamId)
        {
            var update = Builders<Channels>.Update
                .Set(q => q.CurrentStreamId, selected.StreamId)
                .Set(q => q.ModifiedMoment, DateTime.UtcNow);

            await _channelRepository.FindOneAndUpdateAsync(q => q.Id == channel.Id, update);
        }

        return new StreamPlaybackResult
        {
            ChannelId = channel.ChannelId,
            StreamId = selected.StreamId,
            StreamUri = selected.StreamUri,
            UserAgent = selected.UserAgent,
            Referer = selected.Referer,
            Type = selected.Type,
            Quality = selected.Quality
        };
    }

    public async Task<StreamFilteredResult> ReportStreamFailureAsync(
        StreamReportFailureUpdate update)
    {
        var stream = await _streamRepository.GetByStreamIdAsync(update.StreamId)
                     ?? throw new NotFoundException("Stream not found.");

        if (stream.IsHealthy)
        {
            var updateDef = Builders<Streams>.Update
                .Set(q => q.IsHealthy, false)
                .Set(q => q.LastCheckedMoment, DateTime.UtcNow)
                .Set(q => q.ModifiedMoment, DateTime.UtcNow);

            await _streamRepository.FindOneAndUpdateAsync(q => q.Id == stream.Id, updateDef);

            stream.IsHealthy = false;
        }

        var replacement = await _streamRepository
            .AsQueryable()
            .Where(q => q.ChannelId == stream.ChannelId &&
                        q.StreamId != stream.StreamId &&
                        !q.Inactive &&
                        q.IsHealthy)
            .OrderByDescending(q => q.QualityRank)
            .FirstOrDefaultAsync();

        var channel = await _channelRepository.GetByChannelIdAsync(stream.ChannelId);
        if (channel != null &&
            (channel.CurrentStreamId == stream.StreamId ||
             string.IsNullOrEmpty(channel.CurrentStreamId)))
        {
            var channelUpdate = Builders<Channels>.Update
                .Set(q => q.CurrentStreamId, replacement?.StreamId)
                .Set(q => q.ModifiedMoment, DateTime.UtcNow);

            await _channelRepository.FindOneAndUpdateAsync(q => q.Id == channel.Id, channelUpdate);
        }

        await _eventPublisher.PublishStreamHealthChangedAsync(stream, replacement);

        return MapToResult(stream);
    }

    public async Task<StreamFilteredResult> ActivateAsync(StreamActivateUpdate update)
    {
        var stream = await _streamRepository.GetByStreamIdAsync(update.StreamId)
                     ?? throw new NotFoundException("Stream not found.");

        var updateDef = Builders<Streams>.Update
            .Set(q => q.Inactive, !update.ShouldActivate)
            .Set(q => q.ModifiedMoment, DateTime.UtcNow);

        await _streamRepository.FindOneAndUpdateAsync(q => q.Id == stream.Id, updateDef);

        stream.Inactive = !update.ShouldActivate;

        return MapToResult(stream);
    }

    public async Task<string> DeleteAsync(StreamDeleteUpdate update)
    {
        var stream = await _streamRepository.GetByStreamIdAsync(update.StreamId)
                     ?? throw new NotFoundException("Stream not found.");

        await _streamRepository.DeleteOneAsync(q => q.Id == stream.Id);

        return stream.StreamId;
    }

    #region Helpers

    private static StreamFilteredResult MapToResult(Streams stream)
        => new()
        {
            StreamId = stream.StreamId,
            ChannelId = stream.ChannelId,
            Name = stream.Name,
            StreamUri = stream.StreamUri,
            Type = stream.Type,
            Quality = stream.Quality,
            IsHealthy = stream.IsHealthy,
            Inactive = stream.Inactive,
            LastCheckedMoment = stream.LastCheckedMoment
        };

    #endregion
}
