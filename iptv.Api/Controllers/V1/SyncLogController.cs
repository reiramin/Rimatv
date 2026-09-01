using Asp.Versioning;
using iptv.Services._SyncLog.Contracts;
using iptv.Services._SyncLog.DTOs.Results;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Utilities._Permissions.Constants;
using Utilities.Api;
using Utilities.Attributes;
using Utilities.Filters;
using Utilities.MongoDatabase.Filter;

namespace iptv.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [IgnoreSignature]
    public class SyncLogController(ISyncLogService _syncLogService) : ApiBaseController
    {
        [HttpPost("[action]")]
        [CustomRateLimit]
        [Authorize(Permissions.GetAllSyncLogs)]
        [SwaggerOperation(Summary = "Get synchronization logs (filtered).", Tags = ["SyncLog-Admin"])]
        public async Task<MonjoFilteredResult<SyncLogFilteredResult>> GetAllAsync(MonjoQuery query)
            => await _syncLogService.GetAllAsync(query);

        [HttpGet("[action]")]
        [CustomRateLimit]
        [Authorize(Permissions.GetAllSyncLogs)]
        [SwaggerOperation(Summary = "Get a single synchronization log by public key.", Tags = ["SyncLog-Admin"])]
        public async Task<SyncLogFilteredResult> GetAsync([FromQuery] string publicKey)
            => await _syncLogService.GetByPublicKeyAsync(publicKey);
    }
}
