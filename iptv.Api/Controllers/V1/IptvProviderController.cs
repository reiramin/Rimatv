using Asp.Versioning;
using iptv.Services._IptvProvider.Contracts;
using iptv.Services._IptvProvider.DTOs.Results;
using iptv.Services._IptvProvider.DTOs.Updates;
using iptv.Services._IptvSync.Contracts;
using iptv.Services._IptvSync.DTOs.Results;
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
    public class IptvProviderController(
        IIptvProviderService _iptvProviderService,
        IIptvSyncService _iptvSyncService) : ApiBaseController
    {
        [HttpPost("[action]")]
        // [Authorize(Permissions.CreateIptvProvider)]
        [SwaggerOperation(Summary = "Create a new IPTV provider.", Tags = ["IptvProvider-Admin"])]
        public async Task<IptvProviderFilteredResult> CreateAsync(IptvProviderCreateUpdate update)
            => await _iptvProviderService.CreateAsync(update);

        [HttpPut("[action]")]
        // [Authorize(Permissions.EditIptvProvider)]
        [SwaggerOperation(Summary = "Edit an existing IPTV provider.", Tags = ["IptvProvider-Admin"])]
        public async Task<IptvProviderFilteredResult> EditAsync(IptvProviderEditUpdate update)
            => await _iptvProviderService.EditAsync(update);

        [HttpPost("[action]")]
        // [Authorize(Permissions.GetAllIptvProviders)]
        [SwaggerOperation(Summary = "Get all IPTV providers (filtered).", Tags = ["IptvProvider-Admin"])]
        public async Task<MonjoFilteredResult<IptvProviderFilteredResult>> GetAllAsync(MonjoQuery query)
            => await _iptvProviderService.GetAllAsync(query);

        [HttpPost("[action]")]
        // [Authorize(Permissions.GetAllIptvProviders)]
        [SwaggerOperation(Summary = "Get an IPTV provider by public key.", Tags = ["IptvProvider-Admin"])]
        public async Task<IptvProviderFilteredResult> GetAsync([FromQuery] GetGlobalIdUpdate publicKey)
            => await _iptvProviderService.GetByPublicKeyAsync(publicKey);

        [HttpPut("[action]")]
        // [Authorize(Permissions.EditIptvProvider)]
        [SwaggerOperation(Summary = "Activate or deactivate an IPTV provider.", Tags = ["IptvProvider-Admin"])]
        public async Task<IptvProviderFilteredResult> ActivateAsync([FromQuery] string publicKey,
            [FromQuery] bool shouldActivate)
            => await _iptvProviderService.ActivateAsync(publicKey, shouldActivate);

        [HttpDelete("[action]")]
        // [Authorize(Permissions.DeleteIptvProvider)]
        [SwaggerOperation(Summary = "Delete an IPTV provider.", Tags = ["IptvProvider-Admin"])]
        public async Task<string> DeleteAsync([FromQuery] string publicKey)
            => await _iptvProviderService.DeleteAsync(publicKey);

        [HttpPost("[action]")]
        // [Authorize(Permissions.SyncIptvProvider)]
        [SwaggerOperation(Summary = "Trigger synchronization for all active providers.", Tags = ["IptvProvider-Admin"])]
        public async Task<List<IptvSyncResult>> SyncAllAsync()
            => await _iptvSyncService.SyncAllProvidersAsync();

        [HttpPost("[action]")]
        // [Authorize(Permissions.SyncIptvProvider)]
        [SwaggerOperation(Summary = "Trigger synchronization for a single provider.", Tags = ["IptvProvider-Admin"])]
        public async Task<IptvSyncResult> SyncAsync([FromQuery] GetGlobalIdUpdate publicKey)
            => await _iptvSyncService.SyncProviderAsync(publicKey.Id);
    }
}
