using System.Text.Json;
using System.Text.Json.Serialization;

namespace iptv.Services._Resolver;

/// <summary>One rung of a channel's official-source ladder (resolvers.json).</summary>
public sealed class ResolverEntry
{
    [JsonPropertyName("canonicalId")] public string CanonicalId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }

    // resolve | youtube | clientResolve | officialPlayer (default: resolve, or youtube for YouTubeLive).
    [JsonPropertyName("type")] public string Type { get; set; }

    // HtmlRegex | JsonApi | YouTubeLive; null for officialPlayer.
    [JsonPropertyName("method")] public string Method { get; set; }

    [JsonPropertyName("pageUrl")] public string PageUrl { get; set; }

    // HtmlRegex: .NET regex with a named group "url". JsonApi: JSONPath such as $.data.url.
    [JsonPropertyName("pattern")] public string Pattern { get; set; }
    [JsonPropertyName("apiUrl")] public string ApiUrl { get; set; }

    // Only what the site's own player sends (e.g. Referer).
    [JsonPropertyName("headers")] public Dictionary<string, string> Headers { get; set; } = [];

    [JsonPropertyName("ttlSeconds")] public int TtlSeconds { get; set; }
    [JsonPropertyName("requiredRegion")] public string RequiredRegion { get; set; }
    [JsonPropertyName("ipBound")] public bool IpBound { get; set; }

    [JsonPropertyName("youtubeVideoId")] public string YouTubeVideoId { get; set; }
    [JsonPropertyName("youtubeChannelId")] public string YouTubeChannelId { get; set; }

    [JsonPropertyName("playerUrl")] public string PlayerUrl { get; set; }
    [JsonPropertyName("embeddable")] public bool? Embeddable { get; set; }

    [JsonPropertyName("logo")] public string Logo { get; set; }
    [JsonPropertyName("country")] public string Country { get; set; }
    [JsonPropertyName("verifiedAt")] public string VerifiedAt { get; set; }
    [JsonPropertyName("notes")] public string Notes { get; set; }
}

/// <summary>A channel whose official source cannot be used (DRM, login, none) — documented only.</summary>
public sealed class UnavailableEntry
{
    [JsonPropertyName("canonicalId")] public string CanonicalId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("reason")] public string Reason { get; set; }
    [JsonPropertyName("notes")] public string Notes { get; set; }
}

public sealed class ResolverCatalogRoot
{
    [JsonPropertyName("resolvers")] public List<ResolverEntry> Resolvers { get; set; } = [];
    [JsonPropertyName("unavailable")] public List<UnavailableEntry> Unavailable { get; set; } = [];
}

public static class ResolverMethods
{
    public const string HtmlRegex = "HtmlRegex";
    public const string JsonApi = "JsonApi";
    public const string YouTubeLive = "YouTubeLive";
}

/// <summary>Loads the embedded resolvers.json.</summary>
public static class ResolverCatalog
{
    public const string ResourceSuffix = "resolvers.json";

    public static ResolverCatalogRoot LoadEmbedded()
    {
        var asm = typeof(ResolverCatalog).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("_Resolver." + ResourceSuffix, StringComparison.OrdinalIgnoreCase));
        if (name == null)
            return new ResolverCatalogRoot();

        using var stream = asm.GetManifestResourceStream(name)!;
        return Parse(stream);
    }

    public static ResolverCatalogRoot Parse(Stream json)
        => JsonSerializer.Deserialize<ResolverCatalogRoot>(json,
               new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip })
           ?? new ResolverCatalogRoot();
}
