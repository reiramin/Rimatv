using iptv.Domain.Collections;

namespace iptv.Services._IptvNotifier.Contracts;

public interface IIptvEventPublisher
{
    Task PublishChannelChangedAsync(Channels channel, string changeType,
        CancellationToken cancellationToken = default);

    Task PublishStreamHealthChangedAsync(Streams stream, Streams replacementStream,
        CancellationToken cancellationToken = default);

    Task PublishSyncCompletedAsync(SyncLogs syncLog,
        CancellationToken cancellationToken = default);
}
