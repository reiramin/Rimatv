using System.Security.Cryptography;
using iptv.Services._Common.Settings;
using Microsoft.Extensions.Logging;
using Utilities.Constants;

namespace iptv.Services._Stream.Reporting;

public interface IReporterKeyProvider
{
    /// <summary>Hashed reporter identity for an anonymous client IP (resolved with ClientIp.Resolve).</summary>
    ReporterIdentity ForIp(string clientIp);
}

public class ReporterKeyProvider : IReporterKeyProvider, RegisterMode.ISingletonDependency
{
    private readonly byte[] _secret;

    public ReporterKeyProvider(ReportSettings reportSettings, JwtServiceSettings jwtSettings,
        ILogger<ReporterKeyProvider> logger)
    {
        _secret = ReporterKey.ResolveSecret(reportSettings?.IpHashSecret, jwtSettings?.SignatureKey);

        if (_secret == null)
        {
            // Never fall back to a public constant: anyone could then hash guessed IPs.
            _secret = RandomNumberGenerator.GetBytes(32);
            logger?.LogError(
                "Neither Reports:IpHashSecret nor JwtServiceSettings:SignatureKey is set; anonymous reporter " +
                "keys use a random per-process secret (reports from before a restart are not linked).");
        }
    }

    public ReporterIdentity ForIp(string clientIp) => ReporterKey.For(clientIp, _secret, DateTime.UtcNow);
}
