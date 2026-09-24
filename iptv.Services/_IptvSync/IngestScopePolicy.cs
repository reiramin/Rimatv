using iptv.Domain.Collections;
using iptv.Services._Canonical;
using iptv.Services._Common.Settings;
using iptv.Services._IptvProvider.DTOs.Results;

namespace iptv.Services._IptvSync;

/// <summary>
/// Which provider channels are persisted (§5.2, config Sync:IngestScope).
///   Registry (default): only channels whose canonical id is a registry entry, plus EVERYTHING from
///                       the Persian providers (shayanline, and famelack's ir file).
///   All:                every channel with a playable stream (previous behaviour).
/// The app only consumes curated channels, so Registry cuts DB size, sync time, lite-cache memory
/// and response size.
/// </summary>
public static class IngestScopePolicy
{
    public static bool Keep(IngestScope scope, ChannelRegistryIndex registry, IptvProviders provider,
        ExternalChannel channel, string canonicalId)
    {
        if (scope == IngestScope.All)
            return true;

        if (provider?.Kind == ProviderKind.Resolver || IsPersianSource(provider, channel))
            return true;

        return canonicalId != null && registry.ByCanonicalId.ContainsKey(canonicalId);
    }

    /// <summary>Same rule for an already persisted channel (famelack ir entries carry country IR).</summary>
    public static bool KeepExisting(IngestScope scope, ChannelRegistryIndex registry, IptvProviders provider,
        Channels existing)
    {
        if (scope == IngestScope.All || provider?.Kind == ProviderKind.Resolver)
            return true;

        if (provider?.Name?.StartsWith("shayanline", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        if (provider?.Kind == ProviderKind.FamelackJson &&
            string.Equals(existing.Country, "IR", StringComparison.OrdinalIgnoreCase))
            return true;

        return existing.CanonicalId != null && registry.ByCanonicalId.ContainsKey(existing.CanonicalId);
    }

    public static bool IsPersianSource(IptvProviders provider, ExternalChannel channel)
    {
        if (provider?.Name?.StartsWith("shayanline", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return provider?.Kind == ProviderKind.FamelackJson &&
               channel?.SourceEndpoint?.EndsWith("/ir.json", StringComparison.OrdinalIgnoreCase) == true;
    }
}
