namespace iptv.Api.Utilities.Configurations;

/// <summary>
/// Gzip request bodies are inflated ONLY for the signed probe-results endpoint. Anywhere else a
/// small anonymous gzip body could expand to tens of MB and then be read again by the AntiXss and
/// logging middlewares.
/// </summary>
public static class RequestDecompressionScope
{
    public const string ProbeResultsPath = "/api/v1/Stream/ReportProbeResultsAsync";

    public static bool Applies(HttpContext context)
        => context.Request.Path.Equals(ProbeResultsPath, StringComparison.OrdinalIgnoreCase);
}
