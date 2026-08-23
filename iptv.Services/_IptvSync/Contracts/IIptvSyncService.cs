using iptv.Services._IptvSync.DTOs.Results;

namespace iptv.Services._IptvSync.Contracts;

public interface IIptvSyncService
{
    Task<List<IptvSyncResult>> SyncAllProvidersAsync(CancellationToken cancellationToken = default);

    Task<IptvSyncResult> SyncProviderAsync(string providerPublicKey,
        CancellationToken cancellationToken = default);
}
