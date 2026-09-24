using Asp.Versioning;
using iptv.Services._StreamProbe.Contracts;
using iptv.Services._StreamProbe.DTOs;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Utilities.Api;
using Utilities.Attributes;
using Utilities.Filters;

namespace iptv.Api.Controllers.V1
{
    /// <summary>
    /// Lives in its own controller so it is NOT covered by StreamController's [IgnoreSignature]:
    /// the signature middleware (ApplicationId / Nonce / Signature headers, HMAC-SHA256 of the
    /// nonce with the pre-shared key, nonces single-use for 5 min) protects it.
    /// </summary>
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/Stream")]
    public class StreamProbeController(IStreamProbeService _probeService) : ApiBaseController
    {
        // [IgnoreLogging]: the hourly body can be thousands of items; keep it out of RequestLogs.
        [HttpPost("ReportProbeResultsAsync")]
        [IgnoreLogging]
        [CustomRateLimit(maxAttemptsCount: 20, periodSeconds: 60)]
        [SwaggerOperation(Summary = "Apply deep-probe results from the RimaTv_Data validator (signed; gzip body allowed).",
            Tags = ["Stream"])]
        public async Task<ProbeReportResult> ReportProbeResultsAsync(
            [FromBody] List<ProbeResultItem> items, CancellationToken cancellationToken)
            => await _probeService.ApplyAsync(items, cancellationToken);
    }
}
