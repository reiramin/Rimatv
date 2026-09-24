using Asp.Versioning;
using iptv.Api.Utilities.Filters;
using iptv.Services._Resolver.Contracts;
using iptv.Services._Resolver.DTOs;
using iptv.Services._Stream.Contracts;
using iptv.Services._Stream.DTOs.Results;
using iptv.Services._Stream.DTOs.Updates;
using iptv.Services._Stream.Reporting;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Utilities._Permissions.Constants;
using Utilities.Api;
using Utilities.Attributes;
using Utilities.Extensions;
using Utilities.Filters;
using Utilities.Models.Updates;

namespace iptv.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [IgnoreSignature]
    public class StreamController(
        IStreamService _streamService, IReporterKeyProvider _reporterKeys, IStreamResolverService _resolverService)
        : ApiBaseController
    {
        [HttpPost("[action]")]
        [Authorize]
        [CustomRateLimit]
        [SwaggerOperation(Summary = "Get all active streams of a channel.", Tags = ["Stream"])]
        public async Task<List<StreamFilteredResult>> GetByChannelAsync([FromQuery] GetGlobalIdUpdate channelId)
            => await _streamService.GetByChannelAsync(channelId);

        [HttpPost("[action]")]
        [Authorize]
        [CustomRateLimit]
        [SwaggerOperation(Summary = "Get the playback stream for a channel with automatic healthy fallback.",
            Tags = ["Stream"])]
        public async Task<StreamPlaybackResult> GetPlaybackStreamAsync([FromQuery] GetGlobalIdUpdate channelId)
            => await _streamService.GetPlaybackStreamAsync(channelId);

        // Anonymous: the app has no login. [CustomRateLimit] stays, plus a stricter per-IP limit.
        // Reporters are keyed by SHA256(client IP + daily salt); the raw IP is never stored.
        [HttpPost("[action]")]
        [CustomRateLimit]
        [IpRateLimit(maxRequests: 30, periodSeconds: 600)]
        [SwaggerOperation(Summary = "Report a failing stream; a replacement is selected across all providers of the canonical channel.",
            Tags = ["Stream"])]
        public async Task<StreamReportFailureResult> ReportFailureAsync(StreamReportFailureUpdate update)
            => await _streamService.ReportStreamFailureAsync(
                update, _reporterKeys.ForIp(HttpContext.GetRequestIpv4()));

        // Anonymous, per-IP limited. Fetches the official page server-side (inbound traffic only)
        // and returns the short-lived .m3u8 — video never passes through this server.
        // Explicit "ResolveAsync" (MVC strips the Async suffix from [action]); "Resolve" matches the
        // naming of the other routes.
        [HttpGet("ResolveAsync")]
        [HttpGet("Resolve")]
        [CustomRateLimit(maxAttemptsCount: 30, periodSeconds: 60)]
        [SwaggerOperation(Summary = "Resolve an official tokenized stream (type = resolve) to a playable .m3u8.",
            Tags = ["Stream"])]
        public async Task<StreamResolveResult> ResolveAsync([FromQuery] string streamId, CancellationToken cancellationToken)
            => await _resolverService.ResolveAsync(
                streamId, ReporterKey.FirstForwardedAddress(HttpContext.GetRequestIpv4()), cancellationToken);

        #region Admin Actions

        [HttpPut("[action]")]
        [Authorize(Permissions.EditStream)]
        [SwaggerOperation(Summary = "Activate or deactivate a stream.", Tags = ["Stream-Admin"])]
        public async Task<StreamFilteredResult> ActivateAsync(StreamActivateUpdate update)
            => await _streamService.ActivateAsync(update);

        [HttpDelete("[action]")]
        [Authorize(Permissions.DeleteStream)]
        [SwaggerOperation(Summary = "Delete a stream.", Tags = ["Stream-Admin"])]
        public async Task<string> DeleteAsync(StreamDeleteUpdate update)
            => await _streamService.DeleteAsync(update);

        #endregion
    }
}
