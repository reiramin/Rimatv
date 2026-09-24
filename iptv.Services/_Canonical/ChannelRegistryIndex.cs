using iptv.Domain.Collections;

namespace iptv.Services._Canonical;

/// <summary>
/// Immutable, in-memory view of the Channel Registry used to resolve a provider channel to a single
/// cross-provider canonical id. Pure and deterministic (no I/O) so it can be unit tested directly.
/// Resolution order (AGENT_PROMPT §4):
///   1. iptv-org id / tvg-id (the caller's <c>idHint</c>) — most sources use the iptv-org scheme;
///   2. registry alias lookup by normalized name (disambiguated by country when needed);
///   3. null — the caller then falls back to <c>ext:{provider}:{externalId}</c>.
/// </summary>
public sealed class ChannelRegistryIndex
{
    public IReadOnlyDictionary<string, ChannelRegistry> ByCanonicalId { get; }
    private readonly Dictionary<string, List<ChannelRegistry>> _byAlias;

    private ChannelRegistryIndex(
        Dictionary<string, ChannelRegistry> byCanonicalId,
        Dictionary<string, List<ChannelRegistry>> byAlias)
    {
        ByCanonicalId = byCanonicalId;
        _byAlias = byAlias;
    }

    public static ChannelRegistryIndex Build(IEnumerable<ChannelRegistry> entries)
    {
        var byId = new Dictionary<string, ChannelRegistry>(StringComparer.Ordinal);
        var byAlias = new Dictionary<string, List<ChannelRegistry>>(ChannelNameNormalizer.Comparer);

        foreach (var e in entries)
        {
            if (string.IsNullOrWhiteSpace(e.CanonicalId))
                continue;

            byId[e.CanonicalId] = e;

            foreach (var alias in AliasesOf(e))
            {
                var norm = ChannelNameNormalizer.Normalize(alias);
                if (norm.Length == 0)
                    continue;

                if (!byAlias.TryGetValue(norm, out var list))
                    byAlias[norm] = list = [];

                if (!list.Contains(e))
                    list.Add(e);
            }
        }

        return new ChannelRegistryIndex(byId, byAlias);
    }

    private static IEnumerable<string> AliasesOf(ChannelRegistry e)
    {
        if (!string.IsNullOrWhiteSpace(e.Name)) yield return e.Name;
        if (!string.IsNullOrWhiteSpace(e.NameFa)) yield return e.NameFa;
        foreach (var a in e.Aliases ?? [])
            if (!string.IsNullOrWhiteSpace(a))
                yield return a;
    }

    /// <summary>Resolves a canonical id or returns null when the channel is unknown to the registry.</summary>
    public string ResolveCanonical(string idHint, string name, string sourceCountry)
    {
        if (!string.IsNullOrWhiteSpace(idHint))
            return idHint.Trim();

        var entry = ResolveEntryByName(name, sourceCountry);
        return entry?.CanonicalId;
    }

    public ChannelRegistry ResolveEntryByName(string name, string sourceCountry)
    {
        var norm = ChannelNameNormalizer.Normalize(name);
        if (norm.Length == 0 || !_byAlias.TryGetValue(norm, out var candidates) || candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        // Ambiguous alias: prefer a country match, then the best (lowest) curated rank, then id order.
        if (!string.IsNullOrWhiteSpace(sourceCountry))
        {
            var byCountry = candidates
                .Where(c => string.Equals(c.SourceCountry, sourceCountry, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(c.CuratedCountry, sourceCountry, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (byCountry.Count == 1)
                return byCountry[0];
            if (byCountry.Count > 1)
                candidates = byCountry;
        }

        return candidates
            .OrderBy(c => c.CuratedRank == 0 ? int.MaxValue : c.CuratedRank)
            .ThenBy(c => c.CanonicalId, StringComparer.Ordinal)
            .First();
    }
}
