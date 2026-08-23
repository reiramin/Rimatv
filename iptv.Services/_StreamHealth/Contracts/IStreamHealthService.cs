using iptv.Services._StreamHealth.DTOs.Results;

namespace iptv.Services._StreamHealth.Contracts;

public interface IStreamHealthService
{
    Task<StreamHealthCheckResult> CheckStaleStreamsAsync(int limit = 200,
        CancellationToken cancellationToken = default);

    Task<StreamHealthCheckResult> CheckChannelStreamsAsync(string channelId,
        CancellationToken cancellationToken = default);

    Task<bool> CheckStreamAsync(string streamId,
        CancellationToken cancellationToken = default);
}
