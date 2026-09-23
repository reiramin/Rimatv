using iptv.Services._Canonical;
using iptv.Services._ChannelRegistry.DTOs;
using Utilities.Models.Updates;
using Utilities.MongoDatabase.Filter;

namespace iptv.Services._ChannelRegistry.Contracts;

public interface IChannelRegistryService
{
    // Cached, cross-provider canonical index used by sync and selection.
    Task<ChannelRegistryIndex> GetIndexAsync(CancellationToken cancellationToken = default);

    void InvalidateIndex();

    Task<MonjoFilteredResult<ChannelRegistryResult>> GetAllAsync(MonjoQuery query);

    Task<ChannelRegistryResult> CreateAsync(ChannelRegistryCreateUpdate update);

    Task<ChannelRegistryResult> EditAsync(ChannelRegistryEditUpdate update);

    Task<ChannelRegistryResult> ActivateAsync(ChannelRegistryActivateUpdate update);

    Task<string> DeleteAsync(GetGlobalIdUpdate canonicalId);
}
