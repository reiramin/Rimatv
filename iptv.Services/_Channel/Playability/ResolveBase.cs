namespace iptv.Services._Channel.Playability;

/// <summary>
/// Base URL for emitted resolveUrl values. AppSettings:BaseUrl wins when it is a real absolute URL;
/// when it is empty or still the template placeholder (your-app.onrender.com), the incoming request's
/// scheme and host are used instead (honouring X-Forwarded-Proto behind Render's proxy). For the
/// list the workflow fetches, that host is the Render host.
/// </summary>
public static class ResolveBase
{
    public const string Placeholder = "your-app.onrender.com";

    public static bool IsUsable(string configured)
        => !string.IsNullOrWhiteSpace(configured) &&
           !configured.Contains(Placeholder, StringComparison.OrdinalIgnoreCase) &&
           Uri.TryCreate(configured.Trim(), UriKind.Absolute, out var u) &&
           (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    public static string Choose(string configured, string forwardedProto, string scheme, string host)
    {
        if (IsUsable(configured))
            return configured.Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(host))
            return null;

        var proto = forwardedProto?.Split(',')[0].Trim();
        if (proto is not ("http" or "https"))
            proto = string.IsNullOrWhiteSpace(scheme) ? "https" : scheme;

        return $"{proto}://{host}";
    }

    /// <summary>Startup warning text when BaseUrl is not usable, else null.</summary>
    public static string StartupWarning(string configured)
        => IsUsable(configured)
            ? null
            : "AppSettings:BaseUrl is empty or still the placeholder '" + Placeholder +
              "'; resolveUrl values are built from each request's host instead. Set AppSettings__BaseUrl.";
}
