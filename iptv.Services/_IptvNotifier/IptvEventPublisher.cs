using iptv.Domain.Collections;
using iptv.Services._IptvNotifier.Contracts;
using iptv.Services._IptvNotifier.DTOs.Results;
using Microsoft.AspNetCore.SignalR;
using Utilities.Constants;

namespace iptv.Services._IptvNotifier;

public class IptvEventPublisher(IHubContext<IptvHub> _hubContext)
    : IIptvEventPublisher, RegisterMode.IScopedDependency
{
    private const string ChannelGroupPrefix = "channel-";

    public async Task PublishChannelChangedAsync(Channels channel, string changeType,
        CancellationToken cancellationToken = default)
    {
        var payload = new ChannelChangedEvent
        {
            ChannelId = channel.ChannelId,
            Name = channel.Name,
            ChangeType = changeType
        };

        await _hubContext.Clients
            .Group(ChannelGroupPrefix + channel.ChannelId)
            .SendAsync("ChannelChanged", payload, cancellationToken);
    }

    public async Task PublishStreamHealthChangedAsync(Streams stream, Streams replacementStream,
        CancellationToken cancellationToken = default)
    {
        var payload = new StreamHealthChangedEvent
        {
            StreamId = stream.StreamId,
            ChannelId = stream.ChannelId,
            IsHealthy = stream.IsHealthy,
            ReplacementStreamId = replacementStream?.StreamId,
            ReplacementStreamUri = replacementStream?.StreamUri
        };

        await _hubContext.Clients
            .Group(ChannelGroupPrefix + stream.ChannelId)
            .SendAsync("StreamHealthChanged", payload, cancellationToken);
    }

    public async Task PublishSyncCompletedAsync(SyncLogs syncLog,
        CancellationToken cancellationToken = default)
    {
        var payload = new SyncCompletedEvent
        {
            ProviderPublicKey = syncLog.ProviderPublicKey,
            ProviderName = syncLog.ProviderName,
            Status = syncLog.Status.ToString(),
            InsertedChannels = syncLog.InsertedChannels,
            UpdatedChannels = syncLog.UpdatedChannels,
            DeactivatedChannels = syncLog.DeactivatedChannels,
            InsertedStreams = syncLog.InsertedStreams,
            UpdatedStreams = syncLog.UpdatedStreams,
            DeactivatedStreams = syncLog.DeactivatedStreams
        };

        await _hubContext.Clients
            .Group(syncLog.ProviderPublicKey)
            .SendAsync("SyncCompleted", payload, cancellationToken);
    }
}
