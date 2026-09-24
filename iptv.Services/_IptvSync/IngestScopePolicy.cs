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
    /// <summary>Result of <see cref="Plan"/>.</summary>
    public sealed record ScopePlan(List<Channels> ToDeactivate, HashSet<string> OutOfScopeChannelIds);

    /// <summary>
    /// Whether a freshly fetched channel is kept, decided with its NEWLY computed canonical id.
    /// Scope is not applied when the registry is empty (e.g. the seed failed).
    /// </summary>
    public static bool Keep(IngestScope scope, ChannelRegistryIndex registry, IptvProviders provider,
        ExternalChannel channel, string canonicalId)
    {
        if (scope == IngestScope.All || registry.ByCanonicalId.Count == 0)
            return true;

        if (provider?.Kind == ProviderKind.Resolver || IsPersianSource(provider, channel))
            return true;

        return canonicalId != null && registry.ByCanonicalId.ContainsKey(canonicalId);
    }

    /// <summary>
    /// Existing channels that this sync fetched but decided to leave out of scope. They are
    /// DEACTIVATED (never deleted: ChannelId / StreamId, probe results, reports and AdminDisabled
    /// survive if they come back into scope). A channel kept by this sync is never touched, whatever
    /// canonical id is stored on it. Channels that were not fetched at all are left to the normal
    /// (guarded) deactivation path.
    /// </summary>
    public static ScopePlan Plan(IngestScope scope, int registryCount,
        IEnumerable<string> keptExternalIds, IEnumerable<string> ingestableExternalIds, IEnumerable<Channels> existing)
    {
        if (scope == IngestScope.All || registryCount == 0)
            return new ScopePlan([], new HashSet<string>(StringComparer.Ordinal));

        var kept = new HashSet<string>(keptExternalIds, StringComparer.Ordinal);
        var outOfScope = new HashSet<string>(ingestableExternalIds.Where(id => !kept.Contains(id)), StringComparer.Ordinal);

        var docs = existing.Where(c => c.ExternalId != null && outOfScope.Contains(c.ExternalId)).ToList();
        return new ScopePlan(
            docs.Where(c => !c.Inactive).ToList(),
            docs.Select(c => c.ChannelId).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// Everything from the Persian providers is always kept: shayanline, and famelack's ir file.
    /// ONE rule for new (ExternalChannel) and persisted (Channels) documents, via the SourceTag.
    /// </summary>
    public static bool IsPersianSource(IptvProviders provider, string sourceTag)
    {
        if (provider?.Name?.StartsWith("shayanline", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return provider?.Kind == ProviderKind.FamelackJson &&
               string.Equals(sourceTag, "famelack:ir", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPersianSource(IptvProviders provider, ExternalChannel channel)
        => IsPersianSource(provider, channel?.SourceTag);

    public static bool IsPersianSource(IptvProviders provider, Channels channel)
        => IsPersianSource(provider, channel?.SourceTag);
}
