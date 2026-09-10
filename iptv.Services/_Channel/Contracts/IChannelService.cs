using iptv.Services._Channel.DTOs.Results;
using iptv.Services._Channel.DTOs.Updates;
using Utilities.Models.Updates;
using Utilities.MongoDatabase.Filter;
using Utilities.Utilities;

namespace iptv.Services._Channel.Contracts;

public interface IChannelService
{
    Task<MonjoFilteredResult<ChannelBasicResult>> GetAllAsync(MonjoQuery query);

    Task<List<ChannelWithStreamResult>> GetCuratedListWithStreamAsync(
        CancellationToken cancellationToken = default);

    Task<List<AllChannelWithStreamResult>> GetAllUnpagedWithStreamAsync(
        CancellationToken cancellationToken = default);
    
    Task<MonjoFilteredResult<ChannelBasicResult>>
        GetChannelsFilteredAsync(MonjoQuery query, GetChannelsFilteredUpdate update);

    Task<ManualPaginationResult<ChannelResult>> GetChannelWithSearchAsync(GetAllChannelUpdate update);

    Task<MonjoFilteredResult<ChannelFilteredResult>> GetAllForAdminAsync(MonjoQuery query);

    Task<ChannelBasicResult> GetByChannelIdAsync(GetGlobalIdUpdate channelId);

    Task<ChannelFilteredResult> ActivateAsync(ChannelActivateUpdate update);

    Task<string> DeleteAsync(ChannelDeleteUpdate update);
}