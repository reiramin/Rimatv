using System.Text.Json.Serialization;

namespace iptv.Services._Resolver.DTOs;

/// <summary>Response of Stream/ResolveAsync — deliberately a few hundred bytes.</summary>
public class StreamResolveResult
{
    public bool Found { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Url { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string UserAgent { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Referer { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? ExpiresAt { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string RequiredRegion { get; set; }

    // RESOLVE_FAILED | USE_VPN | IP_BOUND
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string ErrorCode { get; set; }
}

public static class ResolveErrorCodes
{
    public const string ResolveFailed = "RESOLVE_FAILED";
    public const string UseVpn = "USE_VPN";
    public const string IpBound = "IP_BOUND";
}
