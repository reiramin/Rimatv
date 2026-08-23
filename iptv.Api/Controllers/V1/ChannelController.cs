using Asp.Versioning;
using iptv.Services._Channel.Contracts;
using iptv.Services._Channel.DTOs.Results;
using iptv.Services._Channel.DTOs.Updates;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Utilities._Permissions.Constants;
using Utilities.Api;
using Utilities.Attributes;
using Utilities.Filters;
using Utilities.Models.Updates;
using Utilities.MongoDatabase.Filter;
using Utilities.Utilities;

namespace iptv.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [IgnoreSignature]
    public class ChannelController(IChannelService _channelService) : ApiBaseController
    {
        [HttpPost("[action]")]
        [SwaggerOperation(Summary = "Get active channels (filtered, for Flutter/Web).", Tags = ["Channel"])]
        public async Task<MonjoFilteredResult<ChannelFilteredResult>> GetAllAsync(MonjoQuery query)
            => await _channelService.GetAllAsync(query);

        [HttpPost("[action]")]
        [SwaggerOperation(Summary = "Get a channel by its channel id.", Tags = ["Channel"])]
        public async Task<ChannelFilteredResult> GetAsync([FromQuery] GetGlobalIdUpdate channelId)
            => await _channelService.GetByChannelIdAsync(channelId);

        [HttpPost("[action]")]
        [SwaggerOperation(Summary = "Get a channel by filtered", Tags = ["Channel"])]
        public async Task<MonjoFilteredResult<ChannelFilteredResult>>
            GetChannelsFilteredAsync(MonjoQuery query,[FromQuery] GetChannelsFilteredUpdate update)
            => await _channelService.GetChannelsFilteredAsync(
                query, update);

        [HttpPost("[action]")]
        [SwaggerOperation(Summary = "Get a channel by Search.", Tags = ["Channel"])]
        public async Task<ManualPaginationResult<ChannelResult>> GetChannelWithSearchAsync(
            [FromQuery] GetAllChannelUpdate update)
            => await _channelService.GetChannelWithSearchAsync(update);

        #region Admin Actions

        [HttpPost("[action]")]
        // [global::Utilities.Filters.Authorize(Permissions.GetAllChannels)]
        [SwaggerOperation(Summary = "Get all channels including inactive ones.", Tags = ["Channel-Admin"])]
        public async Task<MonjoFilteredResult<ChannelFilteredResult>> GetAllForAdminAsync(MonjoQuery query)
            => await _channelService.GetAllForAdminAsync(query);

        [HttpPut("[action]")]
        // [global::Utilities.Filters.Authorize(Permissions.EditChannel)]
        [SwaggerOperation(Summary = "Activate or deactivate a channel.", Tags = ["Channel-Admin"])]
        public async Task<ChannelFilteredResult> ActivateAsync(ChannelActivateUpdate update)
            => await _channelService.ActivateAsync(update);

        [HttpDelete("[action]")]
        // [global::Utilities.Filters.Authorize(Permissions.DeleteChannel)]
        [SwaggerOperation(Summary = "Delete a channel.", Tags = ["Channel-Admin"])]
        public async Task<string> DeleteAsync(ChannelDeleteUpdate update)
            => await _channelService.DeleteAsync(update);

        #endregion
    }
}