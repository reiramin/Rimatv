using iptv.Services._Channel.DTOs.Results;
using iptv.Services._Channel.DTOs.Updates;
using Utilities.Models.Updates;
using Utilities.MongoDatabase.Filter;
using Utilities.Utilities;

namespace iptv.Services._Channel.Contracts;

public interface IChannelService
{
    Task<MonjoFilteredResult<ChannelFilteredResult>> GetAllAsync(MonjoQuery query);

    Task<MonjoFilteredResult<ChannelWithStreamResult>> GetAllWithStreamAsync(MonjoQuery query);
    
    Task<MonjoFilteredResult<ChannelFilteredResult>>
        GetChannelsFilteredAsync(MonjoQuery query, GetChannelsFilteredUpdate update);

    Task<ManualPaginationResult<ChannelResult>> GetChannelWithSearchAsync(GetAllChannelUpdate update);

    Task<MonjoFilteredResult<ChannelFilteredResult>> GetAllForAdminAsync(MonjoQuery query);

    Task<ChannelFilteredResult> GetByChannelIdAsync(GetGlobalIdUpdate channelId);

    Task<ChannelFilteredResult> ActivateAsync(ChannelActivateUpdate update);

    Task<string> DeleteAsync(ChannelDeleteUpdate update);
}