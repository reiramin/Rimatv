using iptv.Services._Common.Settings;
using Utilities.Constants;

namespace iptv.Services._Stream.Reporting;

public interface IReporterKeyProvider
{
    /// <summary>Hashed reporter key for an anonymous client IP (resolved with ClientIp.Resolve).</summary>
    string ForIp(string clientIp);
}

public class ReporterKeyProvider(ReportSettings _reportSettings, JwtServiceSettings _jwtSettings)
    : IReporterKeyProvider, RegisterMode.ISingletonDependency
{
    private readonly byte[] _secret = ReporterKey.ResolveSecret(
        _reportSettings?.IpHashSecret, _jwtSettings?.SignatureKey);

    public string ForIp(string clientIp) => ReporterKey.FromIp(clientIp, _secret, DateTime.UtcNow);
}
