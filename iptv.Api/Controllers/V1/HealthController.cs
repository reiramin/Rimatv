using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Utilities.Api;
using Utilities.Attributes;

namespace iptv.Api.Controllers.V1;

[ApiController]
[ApiVersion("1")]
[Route("health")]
[IgnoreSignature]
public class HealthController : ApiBaseController
{
    [HttpGet]
    [IgnoreLogging]
    public IActionResult Get()
        => Ok(new { status = "ok", moment = DateTime.UtcNow });
}