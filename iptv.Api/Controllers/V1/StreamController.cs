using Asp.Versioning;
using iptv.Services._Stream.Contracts;
using iptv.Services._Stream.DTOs.Results;
using iptv.Services._Stream.DTOs.Updates;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Utilities._Permissions.Constants;
using Utilities.Api;
using Utilities.Attributes;
using Utilities.Filters;
using Utilities.Models.Updates;

namespace iptv.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [IgnoreSignature]
    public class StreamController(IStreamService _streamService) : ApiBaseController
    {
        [HttpPost("[action]")]
        [Authorize]
        [SwaggerOperation(Summary = "Get all active streams of a channel.", Tags = ["Stream"])]
        public async Task<List<StreamFilteredResult>> GetByChannelAsync([FromQuery] GetGlobalIdUpdate channelId)
            => await _streamService.GetByChannelAsync(channelId);

        [HttpPost("[action]")]
        [Authorize]
        [SwaggerOperation(Summary = "Get the playback stream for a channel with automatic healthy fallback.",
            Tags = ["Stream"])]
        public async Task<StreamPlaybackResult> GetPlaybackStreamAsync([FromQuery] GetGlobalIdUpdate channelId)
            => await _streamService.GetPlaybackStreamAsync(channelId);

        [HttpPost("[action]")]
        [Authorize]
        [SwaggerOperation(Summary = "Report a failing stream; another healthy stream is selected automatically.",
            Tags = ["Stream"])]
        public async Task<StreamFilteredResult> ReportFailureAsync(StreamReportFailureUpdate update)
            => await _streamService.ReportStreamFailureAsync(update);

        #region Admin Actions

        [HttpPut("[action]")]
        // [Authorize(Permissions.EditStream)]
        [SwaggerOperation(Summary = "Activate or deactivate a stream.", Tags = ["Stream-Admin"])]
        public async Task<StreamFilteredResult> ActivateAsync(StreamActivateUpdate update)
            => await _streamService.ActivateAsync(update);

        [HttpDelete("[action]")]
        // [Authorize(Permissions.DeleteStream)]
        [SwaggerOperation(Summary = "Delete a stream.", Tags = ["Stream-Admin"])]
        public async Task<string> DeleteAsync(StreamDeleteUpdate update)
            => await _streamService.DeleteAsync(update);

        #endregion
    }
}
