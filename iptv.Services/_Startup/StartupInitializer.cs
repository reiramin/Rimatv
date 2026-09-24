using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._Canonical;
using iptv.Services._ChannelRegistry;
using iptv.Services._ChannelRegistry.Seed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Utilities.Constants;

namespace iptv.Services._Startup;

/// <summary>
/// One-time, idempotent startup work: dedupe legacy duplicate channels/streams, create indexes,
/// seed the Channel Registry (from the embedded JSON) and the free IPTV providers.
/// </summary>
public class StartupInitializer(IServiceProvider _serviceProvider, ILogger<StartupInitializer> _logger)
    : IHostedService, RegisterMode.IHostedDependency
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var channelRepo = sp.GetRequiredService<IChannelRepository>();
        var streamRepo = sp.GetRequiredService<IStreamRepository>();
        var providerRepo = sp.GetRequiredService<IIptvProviderRepository>();
        var registryRepo = sp.GetRequiredService<IChannelRegistryRepository>();
        var migrationRepo = sp.GetRequiredService<IMigrationRepository>();
        var config = sp.GetRequiredService<IConfiguration>();

        // Loading every channel and stream is expensive on the free host, which restarts often:
        // dedupe runs once (marker "dedupe-v1"), or again when a unique index hits duplicate keys.
        await SafeStep("dedupe", () => DedupeMigration.RunOnceAsync(
            migrationRepo, () => DedupeAsync(channelRepo, streamRepo, cancellationToken), cancellationToken));
        await SafeStep("indexes", () => CreateIndexesAsync(
            channelRepo, streamRepo, registryRepo,
            () => DedupeAsync(channelRepo, streamRepo, cancellationToken), cancellationToken));
        await SafeStep("registry-seed", () => SeedRegistryAsync(registryRepo, cancellationToken));

        if (config.GetValue("ProviderSeed:Enabled", true))
            await SafeStep("provider-seed", () => SeedProvidersAsync(providerRepo, cancellationToken));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SafeStep(string name, Func<Task> action)
    {
        try
        {
            await action();
            _logger.LogInformation("Startup step '{Step}' completed.", name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup step '{Step}' failed (continuing).", name);
        }
    }

    #region Dedupe (keep oldest, repoint streams)

    private static async Task DedupeAsync(
        IChannelRepository channelRepo, IStreamRepository streamRepo, CancellationToken ct)
    {
        // Channels first: keep the oldest per (provider, externalId); repoint streams of the losers.
        var channels = await channelRepo.FilterByAsync(_ => true, ct);
        foreach (var group in channels
                     .GroupBy(c => (c.ProviderPublicKey, c.ExternalId))
                     .Where(g => g.Count() > 1))
        {
            var ordered = group.OrderBy(c => c.CreatedMoment).ToList();
            var keeper = ordered[0];
            foreach (var loser in ordered.Skip(1))
            {
                await streamRepo.UpdateManyAsync(
                    s => s.ChannelId == loser.ChannelId,
                    Builders<Streams>.Update.Set(s => s.ChannelId, keeper.ChannelId), ct);
                await channelRepo.DeleteOneAsync(c => c.Id == loser.Id, ct);
            }
        }

        // Streams: keep the oldest per (provider, externalId).
        var streams = await streamRepo.FilterByAsync(_ => true, ct);
        foreach (var group in streams
                     .GroupBy(s => (s.ProviderPublicKey, s.ExternalId))
                     .Where(g => g.Count() > 1))
        {
            foreach (var loser in group.OrderBy(s => s.CreatedMoment).Skip(1))
                await streamRepo.DeleteOneAsync(s => s.Id == loser.Id, ct);
        }
    }

    #endregion

    #region Indexes

    private static async Task CreateIndexesAsync(
        IChannelRepository channelRepo, IStreamRepository streamRepo,
        IChannelRegistryRepository registryRepo, Func<Task> dedupe, CancellationToken ct)
    {
        var dedupeRan = false;

        await TryIndex(() => channelRepo
            .AscendingIndex(c => c.ProviderPublicKey).AscendingIndex(c => c.ExternalId)
            .BuildAsync(new CreateIndexOptions { Unique = true, Name = "ux_channel_provider_external" }));
        await TryIndex(() => channelRepo.AscendingIndex(c => c.ChannelId)
            .BuildAsync(new CreateIndexOptions { Name = "ix_channel_channelId" }));
        await TryIndex(() => channelRepo.AscendingIndex(c => c.CanonicalId)
            .BuildAsync(new CreateIndexOptions { Name = "ix_channel_canonicalId" }));

        await TryIndex(() => streamRepo
            .AscendingIndex(s => s.ProviderPublicKey).AscendingIndex(s => s.ExternalId)
            .BuildAsync(new CreateIndexOptions { Unique = true, Name = "ux_stream_provider_external" }));
        await TryIndex(() => streamRepo.AscendingIndex(s => s.ChannelId)
            .BuildAsync(new CreateIndexOptions { Name = "ix_stream_channelId" }));
        await TryIndex(() => streamRepo
            .AscendingIndex(s => s.Inactive).AscendingIndex(s => s.IsHealthy)
            .BuildAsync(new CreateIndexOptions { Name = "ix_stream_inactive_healthy" }));

        await TryIndex(() => registryRepo.AscendingIndex(r => r.CanonicalId)
            .BuildAsync(new CreateIndexOptions { Unique = true, Name = "ux_registry_canonicalId" }));

        async Task TryIndex(Func<Task> build)
        {
            try { await build(); }
            catch (Exception ex) when (DedupeMigration.IsDuplicateKeyError(ex))
            {
                // Duplicates slipped in (e.g. an older build without the index): dedupe, retry once.
                if (!dedupeRan)
                {
                    dedupeRan = true;
                    await dedupe();
                }

                try { await build(); }
                catch { /* still failing; leave it and retry next boot */ }
            }
            catch { /* an incompatible legacy index already exists; leave it in place */ }
        }
    }

    #endregion

    #region Registry seed

    private static async Task SeedRegistryAsync(IChannelRegistryRepository registryRepo, CancellationToken ct)
    {
        var asm = typeof(ChannelRegistryService).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("channel_registry.seed.json", StringComparison.OrdinalIgnoreCase));
        if (resourceName == null)
            return;

        await using var stream = asm.GetManifestResourceStream(resourceName)!;
        var root = await JsonSerializer.DeserializeAsync<RegistrySeedRoot>(
            stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
        if (root == null)
            return;

        var existing = (await registryRepo.FilterByAsync(_ => true, ct))
            .GroupBy(e => e.CanonicalId)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var inserts = new List<ChannelRegistry>();

        foreach (var (_, entries) in root.Countries)
        foreach (var e in entries)
        {
            if (string.IsNullOrWhiteSpace(e.CanonicalId))
                continue;

            var hash = SeedHash(e);

            if (!existing.TryGetValue(e.CanonicalId, out var current))
            {
                inserts.Add(new ChannelRegistry
                {
                    CanonicalId = e.CanonicalId.Trim(),
                    Name = e.Name,
                    NameFa = e.NameFa,
                    Aliases = e.Aliases ?? [],
                    CuratedCountry = e.CuratedCountry,
                    CuratedRank = e.CuratedRank,
                    Categories = e.Categories ?? [],
                    SourceCountry = e.SourceCountry,
                    RequiresIranianIp = e.RequiresIranianIp,
                    GeoBlockedEverywhere = e.GeoBlockedEverywhere,
                    SeedHash = hash
                });
                continue;
            }

            if (current.SeedHash == hash)
                continue;

            var update = Builders<ChannelRegistry>.Update
                .Set(r => r.Name, e.Name)
                .Set(r => r.NameFa, e.NameFa)
                .Set(r => r.Aliases, e.Aliases ?? [])
                .Set(r => r.CuratedCountry, e.CuratedCountry)
                .Set(r => r.Categories, e.Categories ?? [])
                .Set(r => r.SourceCountry, e.SourceCountry)
                .Set(r => r.RequiresIranianIp, e.RequiresIranianIp)
                .Set(r => r.GeoBlockedEverywhere, e.GeoBlockedEverywhere)
                .Set(r => r.SeedHash, hash)
                .Set(r => r.ModifiedMoment, DateTime.UtcNow);

            // Never overwrite admin edits of CuratedRank (Inactive is admin-only and never touched here).
            if (!current.AdminEdited)
                update = update.Set(r => r.CuratedRank, e.CuratedRank);

            await registryRepo.FindOneAndUpdateAsync(r => r.Id == current.Id, update, ct);
        }

        // Legacy unmatched names -> alias-linkers so future providers auto-merge.
        foreach (var name in root.Meta?.LegacyWhitelistUnmatched ?? [])
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var canonicalId = $"wanted:{ChannelNameNormalizer.Normalize(name)}";
            if (existing.ContainsKey(canonicalId) || inserts.Any(i => i.CanonicalId == canonicalId))
                continue;

            inserts.Add(new ChannelRegistry
            {
                CanonicalId = canonicalId,
                Name = name,
                Aliases = [name],
                CuratedCountry = "intl",
                CuratedRank = 0,
                Categories = [],
                SeedHash = SeedHash(new RegistrySeedEntry { CanonicalId = canonicalId, Name = name })
            });
        }

        if (inserts.Count > 0)
            await registryRepo.InsertManyAsync(inserts, ct);
    }

    private static string SeedHash(RegistrySeedEntry e)
    {
        var raw = string.Join("|",
            e.CanonicalId, e.Name, e.NameFa, e.CuratedCountry, e.CuratedRank,
            string.Join(",", e.Categories ?? []), string.Join(",", e.Aliases ?? []),
            e.SourceCountry, e.RequiresIranianIp, e.GeoBlockedEverywhere);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    #endregion

    #region Provider seed

    private static readonly string[] FamelackCountries =
        ["ir", "tr", "az", "ae", "qa", "af", "in", "iq", "de", "uz", "tm", "am", "sy", "us", "es", "sa", "ca", "pk", "cn", "ru"];

    private static async Task SeedProvidersAsync(IIptvProviderRepository providerRepo, CancellationToken ct)
    {
        var existingNames = (await providerRepo.FilterByAsync(_ => true, ct))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seeds = BuildProviderSeeds();

        var toInsert = seeds.Where(p => !existingNames.Contains(p.Name)).ToList();
        if (toInsert.Count > 0)
            await providerRepo.InsertManyAsync(toInsert, ct);
    }

    private static List<IptvProviders> BuildProviderSeeds()
    {
        var famelackBase = "https://raw.githubusercontent.com/famelack/famelack-channels/main/tv/raw/countries/";
        var famelackEndpoints = FamelackCountries.Select(cc => $"{famelackBase}{cc}.json").ToList();

        var countryM3uEndpoints = FamelackCountries.Select(cc => $"countries/{cc}.m3u").ToList();
        countryM3uEndpoints.Add("languages/fas.m3u");

        return
        [
            new IptvProviders
            {
                Name = "shayanline-iran", Kind = ProviderKind.M3u, Priority = 90,
                ChannelsEndpoint = "https://raw.githubusercontent.com/shayanline/iptv-iran/main/playlists/iran-all-streams.m3u",
                AdditionalEndpoints = ["https://raw.githubusercontent.com/shayanline/iptv-iran/main/playlists/iran.m3u"],
                FetchTimeoutSeconds = 60
            },
            new IptvProviders
            {
                Name = "iptv-org", Kind = ProviderKind.Generic, Priority = 70,
                BaseUrl = "https://iptv-org.github.io/api/",
                FallbackBaseUrl = "https://raw.githubusercontent.com/iptv-org/api/gh-pages/",
                ChannelsEndpoint = "channels.json",
                StreamsEndpoint = "streams.json",
                LogosEndpoint = "logos.json",
                FeedsEndpoint = "feeds.json",
                BlocklistEndpoint = "blocklist.json",
                FetchTimeoutSeconds = 120
            },
            new IptvProviders
            {
                // Official-source ladder (embedded _Resolver/resolvers.json): resolve / youtube /
                // clientResolve / officialPlayer streams for channels without public static streams.
                Name = "resolvers", Kind = ProviderKind.Resolver, Priority = 50,
                FetchTimeoutSeconds = 30
            },
            new IptvProviders
            {
                Name = "zapp-de", Kind = ProviderKind.ZappJson, Priority = 65,
                ChannelsEndpoint = "https://api.zapp.mediathekview.de/v1/channelInfoList",
                FetchTimeoutSeconds = 60
            },
            new IptvProviders
            {
                Name = "free-tv", Kind = ProviderKind.M3u, Priority = 60,
                ChannelsEndpoint = "https://raw.githubusercontent.com/Free-TV/IPTV/master/playlist.m3u8",
                FetchTimeoutSeconds = 60
            },
            new IptvProviders
            {
                Name = "famelack", Kind = ProviderKind.FamelackJson, Priority = 40,
                ChannelsEndpoint = famelackEndpoints[0],
                AdditionalEndpoints = famelackEndpoints.Skip(1).ToList(),
                FetchTimeoutSeconds = 90
            },
            new IptvProviders
            {
                Name = "iptv-org-country-m3u", Kind = ProviderKind.M3u, Priority = 30,
                BaseUrl = "https://iptv-org.github.io/iptv/",
                FallbackBaseUrl = "https://raw.githubusercontent.com/iptv-org/iptv/gh-pages/",
                ChannelsEndpoint = countryM3uEndpoints[0],
                AdditionalEndpoints = countryM3uEndpoints.Skip(1).ToList(),
                FetchTimeoutSeconds = 120
            },
            new IptvProviders
            {
                Name = "pluto", Kind = ProviderKind.Pluto, Priority = 10,
                ChannelsEndpoint = "https://api.pluto.tv/v2/channels.json",
                FetchTimeoutSeconds = 60
            }
        ];
    }

    #endregion
}
