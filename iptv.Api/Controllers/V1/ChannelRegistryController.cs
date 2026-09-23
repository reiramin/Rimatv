using Asp.Versioning;
using iptv.Services._ChannelRegistry.Contracts;
using iptv.Services._ChannelRegistry.DTOs;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Utilities._Permissions.Constants;
using Utilities.Api;
using Utilities.Attributes;
using Utilities.Filters;
using Utilities.Models.Updates;
using Utilities.MongoDatabase.Filter;

namespace iptv.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [IgnoreSignature]
    public class ChannelRegistryController(IChannelRegistryService _registryService) : ApiBaseController
    {
        [HttpPost("[action]")]
        [Authorize(Permissions.GetAllChannels)]
        [CustomRateLimit]
        [SwaggerOperation(Summary = "Get channel registry entries (filtered).", Tags = ["ChannelRegistry-Admin"])]
        public async Task<MonjoFilteredResult<ChannelRegistryResult>> GetAllAsync(MonjoQuery query)
            => await _registryService.GetAllAsync(query);

        [HttpPost("[action]")]
        [Authorize(Permissions.EditChannel)]
        [CustomRateLimit]
        [SwaggerOperation(Summary = "Create a channel registry entry.", Tags = ["ChannelRegistry-Admin"])]
        public async Task<ChannelRegistryResult> CreateAsync(ChannelRegistryCreateUpdate update)
            => await _registryService.CreateAsync(update);

        [HttpPut("[action]")]
        [Authorize(Permissions.EditChannel)]
        [CustomRateLimit]
        [SwaggerOperation(Summary = "Edit a channel registry entry.", Tags = ["ChannelRegistry-Admin"])]
        public async Task<ChannelRegistryResult> EditAsync(ChannelRegistryEditUpdate update)
            => await _registryService.EditAsync(update);

        [HttpPut("[action]")]
        [Authorize(Permissions.EditChannel)]
        [CustomRateLimit]
        [SwaggerOperation(Summary = "Activate or deactivate a channel registry entry.", Tags = ["ChannelRegistry-Admin"])]
        public async Task<ChannelRegistryResult> ActivateAsync(ChannelRegistryActivateUpdate update)
            => await _registryService.ActivateAsync(update);

        [HttpDelete("[action]")]
        [Authorize(Permissions.DeleteChannel)]
        [CustomRateLimit]
        [SwaggerOperation(Summary = "Delete a channel registry entry.", Tags = ["ChannelRegistry-Admin"])]
        public async Task<string> DeleteAsync([FromQuery] GetGlobalIdUpdate canonicalId)
            => await _registryService.DeleteAsync(canonicalId);
    }
}
