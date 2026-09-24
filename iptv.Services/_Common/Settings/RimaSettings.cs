namespace iptv.Services._Common.Settings;

/// <summary>Client-facing options (config section "Client", env Client__*).</summary>
public class ClientSettings
{
    /// <summary>Help page about VPNs, linked from USE_VPN messages. Omitted from outputs when empty.</summary>
    public string VpnHelpUrl { get; set; }
}

/// <summary>
/// Hook for a future censorship-circumvention relay hosted elsewhere (never on Render).
/// Section "Relay", env Relay__BaseUrl / Relay__SigningKey. Both empty = disabled.
/// </summary>
public class RelaySettings
{
    public string BaseUrl { get; set; }
    public string SigningKey { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(SigningKey);
}

public enum IngestScope
{
    /// <summary>Keep only registry channels, plus everything from the Persian providers.</summary>
    Registry = 0,

    /// <summary>Keep every channel with a playable stream (previous behaviour).</summary>
    All = 1
}

/// <summary>Sync options (section "Sync", env Sync__IngestScope).</summary>
public class SyncSettings
{
    public IngestScope IngestScope { get; set; } = IngestScope.Registry;
}

/// <summary>Failure-report options (section "Reports", env Reports__IpHashSecret).</summary>
public class ReportSettings
{
    /// <summary>
    /// Secret for the daily salt of anonymous reporter keys. When empty a key is derived from the
    /// JWT signature key, so reporter keys survive restarts without new configuration.
    /// </summary>
    public string IpHashSecret { get; set; }
}
