using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._Canonical;
using iptv.Services._Channel;
using iptv.Services._ChannelRegistry.Contracts;
using iptv.Services._Common.Settings;
using iptv.Services._IptvNotifier.Contracts;
using iptv.Services._IptvProvider.Contracts;
using iptv.Services._IptvProvider.DTOs.Results;
using iptv.Services._IptvSync.Contracts;
using iptv.Services._IptvSync.DTOs.Results;
using iptv.Services._Stream.Urls;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Utilities.Constants;

namespace iptv.Services._IptvSync;

public class IptvSyncService(
    IIptvProviderRepository _iptvProviderRepository,
    IIptvProviderService _iptvProviderService,
    IChannelRepository _channelRepository,
    IStreamRepository _streamRepository,
    ISyncLogRepository _syncLogRepository,
    IChannelRegistryService _registryService,
    IIptvEventPublisher _eventPublisher,
    SyncSettings _syncSettings,
    ILogger<IptvSyncService> _logger)
    : IIptvSyncService, RegisterMode.IScopedDependency
{
    private const int BulkBatchSize = 500;

    // Process-wide per-provider lock so manual SyncAll/Sync and the scheduler cannot overlap.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _providerLocks = new();

    public async Task<List<IptvSyncResult>> SyncAllProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        var providers = await _iptvProviderService.GetActiveProvidersAsync(cancellationToken);

        var results = new List<IptvSyncResult>();

        foreach (var provider in providers)
        {
            try
            {
                results.Add(await SyncProviderGuardedAsync(provider, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "IPTV sync for provider {ProviderName} failed unexpectedly.", provider.Name);
                results.Add(await CaptureFailedSyncAsync(provider, ex.Message));
            }
        }

        return results;
    }

    public async Task<IptvSyncResult> SyncProviderAsync(
        string providerPublicKey, CancellationToken cancellationToken = default)
    {
        var provider = await _iptvProviderRepository.GetByPublicKeyAsync(providerPublicKey)
                       ?? throw new Utilities.Exceptions.Common.NotFoundException("IPTV provider not found.");

        return await SyncProviderGuardedAsync(provider, cancellationToken);
    }

    #region Concurrency guard

    private async Task<IptvSyncResult> SyncProviderGuardedAsync(
        IptvProviders provider, CancellationToken cancellationToken)
    {
        var gate = _providerLocks.GetOrAdd(provider.PublicKey, _ => new SemaphoreSlim(1, 1));

        if (!await gate.WaitAsync(0, cancellationToken))
        {
            return new IptvSyncResult
            {
                ProviderPublicKey = provider.PublicKey,
                ProviderName = provider.Name,
                Status = "AlreadyRunning",
                Message = "A sync for this provider is already in progress."
            };
        }

        try
        {
            return await SyncProviderInternalAsync(provider, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    #endregion

    #region Sync

    private async Task<IptvSyncResult> SyncProviderInternalAsync(
        IptvProviders provider, CancellationToken cancellationToken)
    {
        var syncLog = new SyncLogs
        {
            ProviderPublicKey = provider.PublicKey,
            ProviderName = provider.Name,
            Status = SyncStatus.Running
        };

        await _syncLogRepository.InsertOneAsync(syncLog, cancellationToken);

        ProviderFetchResult fetched;
        try
        {
            fetched = await _iptvProviderService.FetchAllAsync(provider, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to fetch provider {ProviderName}. Existing data is left untouched.", provider.Name);
            return await CompleteFailedSyncAsync(provider, syncLog, $"Fetch failed: {ex.Message}", cancellationToken);
        }

        var registryIndex = await _registryService.GetIndexAsync(cancellationToken);

        // --- Playable streams grouped by external channel key (only channels with >=1 stream persist). ---
        var playableStreamsByChannel = fetched.Streams
            .Where(s => !string.IsNullOrWhiteSpace(s.Channel) && IsPlayableUrl(s.Url))
            .GroupBy(s => s.Channel.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var channelStats = await SyncChannelsAsync(
            provider, fetched, registryIndex, playableStreamsByChannel, cancellationToken);

        // Map external channel key -> persisted channel (crash-proof against duplicate keys).
        var persistedByExternalId = (await _channelRepository.GetByProviderAsync(provider.PublicKey, cancellationToken))
            .GroupBy(c => c.ExternalId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var streamStats = await SyncStreamsAsync(
            provider, fetched, registryIndex, playableStreamsByChannel, persistedByExternalId,
            cancellationToken);

        var guardTripped = channelStats.GuardTripped || streamStats.GuardTripped;

        syncLog.Status = guardTripped ? SyncStatus.PartialSuccess : SyncStatus.Success;
        if (guardTripped && string.IsNullOrEmpty(syncLog.Message))
            syncLog.Message =
                "Mass-deactivation guard tripped (fetch returned 0 or <50% of active items); nothing was deactivated.";

        syncLog.TotalChannels = channelStats.Total;
        syncLog.InsertedChannels = channelStats.Inserted;
        syncLog.UpdatedChannels = channelStats.Updated;
        syncLog.DeactivatedChannels = channelStats.Deactivated;
        syncLog.TotalStreams = streamStats.Total;
        syncLog.InsertedStreams = streamStats.Inserted;
        syncLog.UpdatedStreams = streamStats.Updated;
        syncLog.DeactivatedStreams = streamStats.Deactivated;
        syncLog.EndMoment = DateTime.UtcNow;

        await _syncLogRepository.ReplaceOneAsync(syncLog, cancellationToken);
        await MarkProviderSyncAsync(provider, syncLog.Status.ToString(), cancellationToken);
        await _eventPublisher.PublishSyncCompletedAsync(syncLog, cancellationToken);

        // Newly synced data changes the user-facing lists.
        ChannelService.InvalidateSharedLiteCache();

        // Free-tier budget (512 MB RAM): log memory per provider so ingest scopes can be compared.
        using (var process = System.Diagnostics.Process.GetCurrentProcess())
            _logger.LogInformation(
                "SyncMemory provider={Provider} scope={Scope} channels={Channels} streams={Streams} " +
                "workingSetMB={WorkingSet:F1} liveHeapAfterLastGcMB={LiveHeap:F1} allocatedHeapMB={Heap:F1}",
                provider.Name, _syncSettings?.IngestScope ?? IngestScope.Registry,
                channelStats.Total, streamStats.Total,
                process.WorkingSet64 / 1048576.0,
                GC.GetGCMemoryInfo().HeapSizeBytes / 1048576.0,
                GC.GetTotalMemory(false) / 1048576.0);

        return MapToResult(provider, syncLog);
    }

    private async Task<SyncStats> SyncChannelsAsync(
        IptvProviders provider,
        ProviderFetchResult fetched,
        ChannelRegistryIndex registryIndex,
        Dictionary<string, List<ExternalStream>> playableStreamsByChannel,
        CancellationToken cancellationToken)
    {
        var stats = new SyncStats();

        var normalized = fetched.Channels
            .Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Name))
            .GroupBy(c => c.Id.Trim(), StringComparer.Ordinal)
            .Select(g => g.First())
            .Where(c => playableStreamsByChannel.ContainsKey(c.Id.Trim()))   // >=1 playable stream
            .Where(c => IsIngestable(c, fetched.BlockedChannelIds, registryIndex))
            .Select(c => (External: c, Doc: NormalizeChannel(provider, c, registryIndex)))
            .Where(x => IngestScopePolicy.Keep(_syncSettings?.IngestScope ?? IngestScope.Registry,
                registryIndex, provider, x.External, x.Doc.CanonicalId))
            .Select(x => x.Doc)
            .ToList();

        stats.Total = normalized.Count;

        var normalizedByExternalId = normalized
            .GroupBy(c => c.ExternalId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var existing = await _channelRepository.GetByProviderAsync(provider.PublicKey, cancellationToken);

        // Registry scope: channels (and their streams) outside the scope are deleted, not merely
        // deactivated, so the M0 storage is actually freed. Only after a non-empty fetch.
        var scope = _syncSettings?.IngestScope ?? IngestScope.Registry;
        if (scope == IngestScope.Registry && normalized.Count > 0)
        {
            var outOfScope = existing
                .Where(c => !IngestScopePolicy.KeepExisting(scope, registryIndex, provider, c))
                .ToList();

            if (outOfScope.Count > 0)
            {
                await DeleteChannelsWithStreamsAsync(outOfScope, cancellationToken);
                stats.Deleted = outOfScope.Count;
                var removed = outOfScope.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
                existing = existing.Where(c => !removed.Contains(c.Id)).ToList();
            }
        }

        var existingByExternalId = existing
            .GroupBy(c => c.ExternalId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var writes = new List<WriteModel<Channels>>();

        foreach (var doc in normalizedByExternalId.Values)
        {
            if (!existingByExternalId.TryGetValue(doc.ExternalId, out var existingDoc))
            {
                writes.Add(new InsertOneModel<Channels>(doc));
                stats.Inserted++;
                continue;
            }

            if (existingDoc.DataHash == doc.DataHash && !existingDoc.Inactive)
                continue;

            var update = Builders<Channels>.Update
                .Set(q => q.Name, doc.Name)
                .Set(q => q.ImageUri, doc.ImageUri)
                .Set(q => q.Country, doc.Country)
                .Set(q => q.Category, doc.Category)
                .Set(q => q.CanonicalId, doc.CanonicalId)
                .Set(q => q.Feed, doc.Feed)
                .Set(q => q.Languages, doc.Languages)
                .Set(q => q.Labels, doc.Labels)
                .Set(q => q.DataHash, doc.DataHash)
                .Set(q => q.Inactive, false)                 // AdminDisabled is intentionally NOT touched
                .Set(q => q.ModifiedMoment, DateTime.UtcNow);

            writes.Add(new UpdateOneModel<Channels>(
                Builders<Channels>.Filter.Eq(q => q.Id, existingDoc.Id), update));
            stats.Updated++;
        }

        // Mass-deactivation guard.
        var activeExisting = existing.Count(c => !c.Inactive);
        stats.GuardTripped = ShouldSkipDeactivation(normalized.Count, activeExisting);

        if (!stats.GuardTripped)
        {
            var removedIds = existing
                .Where(c => !c.Inactive && !normalizedByExternalId.ContainsKey(c.ExternalId))
                .Select(c => c.Id)
                .ToList();

            foreach (var id in removedIds)
                writes.Add(new UpdateOneModel<Channels>(
                    Builders<Channels>.Filter.Eq(q => q.Id, id),
                    Builders<Channels>.Update
                        .Set(q => q.Inactive, true)
                        .Set(q => q.ModifiedMoment, DateTime.UtcNow)));

            stats.Deactivated = removedIds.Count;
        }

        await BulkWriteAsync(_channelRepository, writes, cancellationToken);
        return stats;
    }

    private async Task<SyncStats> SyncStreamsAsync(
        IptvProviders provider,
        ProviderFetchResult fetched,
        ChannelRegistryIndex registryIndex,
        Dictionary<string, List<ExternalStream>> playableStreamsByChannel,
        Dictionary<string, Channels> persistedByExternalId,
        CancellationToken cancellationToken)
    {
        var stats = new SyncStats();

        var canonicalByExternalId = persistedByExternalId.ToDictionary(
            kv => kv.Key, kv => kv.Value.CanonicalId, StringComparer.Ordinal);

        var normalized = new List<Streams>();
        foreach (var (channelKey, streams) in playableStreamsByChannel)
        {
            if (!persistedByExternalId.TryGetValue(channelKey, out var persisted))
                continue; // channel was not persisted (filtered out)

            var reg = ResolveRegistryEntryForKey(channelKey, canonicalByExternalId, registryIndex);
            var requiresIrByRegistry = reg?.RequiresIranianIp == true;

            foreach (var s in streams)
                normalized.Add(NormalizeStream(provider, channelKey, persisted, reg, s, requiresIrByRegistry));
        }

        // Stream ExternalId already includes the channel key; dedupe crash-proof.
        var normalizedByExternalId = normalized
            .GroupBy(s => s.ExternalId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        stats.Total = normalizedByExternalId.Count;

        var existing = await _streamRepository.GetByProviderAsync(provider.PublicKey, cancellationToken);
        var existingByExternalId = existing
            .GroupBy(s => s.ExternalId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var writes = new List<WriteModel<Streams>>();

        foreach (var doc in normalizedByExternalId.Values)
        {
            if (!existingByExternalId.TryGetValue(doc.ExternalId, out var existingDoc))
            {
                writes.Add(new InsertOneModel<Streams>(doc));   // new streams get neutral (healthy) state
                stats.Inserted++;
                continue;
            }

            if (existingDoc.DataHash == doc.DataHash &&
                !existingDoc.Inactive &&
                existingDoc.ChannelId == doc.ChannelId)
                continue;

            // Preserve IsHealthy, AdminDisabled, probe results and client-failure state on existing streams.
            var update = Builders<Streams>.Update
                .Set(q => q.Name, doc.Name)
                .Set(q => q.ChannelId, doc.ChannelId)
                .Set(q => q.StreamUri, doc.StreamUri)
                .Set(q => q.UserAgent, doc.UserAgent)
                .Set(q => q.Referer, doc.Referer)
                .Set(q => q.Type, doc.Type)
                .Set(q => q.Quality, doc.Quality)
                .Set(q => q.QualityRank, doc.QualityRank)
                .Set(q => q.IsAdaptive, doc.IsAdaptive)
                .Set(q => q.Feed, doc.Feed)
                .Set(q => q.Languages, doc.Languages)
                .Set(q => q.ServerProbeUnreliable, doc.ServerProbeUnreliable)
                .Set(q => q.RequiredRegion, doc.RequiredRegion)
                .Set(q => q.RelayEligible, doc.RelayEligible)
                .Set(q => q.PageUrl, doc.PageUrl)
                .Set(q => q.ResolveMethod, doc.ResolveMethod)
                .Set(q => q.ResolvePattern, doc.ResolvePattern)
                .Set(q => q.ResolveApiUrl, doc.ResolveApiUrl)
                .Set(q => q.ResolveBaseUrl, doc.ResolveBaseUrl)
                .Set(q => q.ResolveHeaders, doc.ResolveHeaders)
                .Set(q => q.ResolveTtlSeconds, doc.ResolveTtlSeconds)
                .Set(q => q.IpBound, doc.IpBound)
                .Set(q => q.PlayerUrl, doc.PlayerUrl)
                .Set(q => q.Embeddable, doc.Embeddable)
                .Set(q => q.DataHash, doc.DataHash)
                .Set(q => q.Inactive, false)
                .Set(q => q.ModifiedMoment, DateTime.UtcNow);

            writes.Add(new UpdateOneModel<Streams>(
                Builders<Streams>.Filter.Eq(q => q.Id, existingDoc.Id), update));
            stats.Updated++;
        }

        var activeExisting = existing.Count(s => !s.Inactive);
        stats.GuardTripped = ShouldSkipDeactivation(normalizedByExternalId.Count, activeExisting);

        if (!stats.GuardTripped)
        {
            var removedIds = existing
                .Where(s => !s.Inactive && !normalizedByExternalId.ContainsKey(s.ExternalId))
                .Select(s => s.Id)
                .ToList();

            foreach (var id in removedIds)
                writes.Add(new UpdateOneModel<Streams>(
                    Builders<Streams>.Filter.Eq(q => q.Id, id),
                    Builders<Streams>.Update
                        .Set(q => q.Inactive, true)
                        .Set(q => q.ModifiedMoment, DateTime.UtcNow)));

            stats.Deactivated = removedIds.Count;
        }

        await BulkWriteAsync(_streamRepository, writes, cancellationToken);
        return stats;
    }

    #endregion

    #region Normalization

    private static Channels NormalizeChannel(
        IptvProviders provider, ExternalChannel external, ChannelRegistryIndex registryIndex)
    {
        var externalId = external.Id.Trim();
        var name = external.Name.Trim();
        var country = (external.Country ?? string.Empty).Trim().ToUpperInvariant();
        var category = external.Categories?.FirstOrDefault()?.Trim() ?? "General";
        var imageUri = external.Logo?.Trim() ?? string.Empty;
        var languages = (external.Languages ?? []).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var labels = (external.Labels ?? []).ToList();

        var canonical = ResolveCanonical(provider, external, registryIndex);

        return new Channels
        {
            ProviderPublicKey = provider.PublicKey,
            ExternalId = externalId,
            CanonicalId = canonical,
            Name = name,
            Country = country,
            Category = category,
            ImageUri = imageUri,
            Feed = external.Feed,
            Languages = languages,
            Labels = labels,
            DataHash = ComputeHash(externalId, canonical, name, country, category, imageUri,
                external.Feed ?? "", string.Join(",", languages))
        };
    }

    private static string ResolveCanonical(
        IptvProviders provider, ExternalChannel external, ChannelRegistryIndex registryIndex)
    {
        var hint = !string.IsNullOrWhiteSpace(external.CanonicalIdHint)
            ? external.CanonicalIdHint
            : external.TvgId;

        return registryIndex.ResolveCanonical(hint, external.Name, external.Country)
               ?? $"ext:{provider.PublicKey}:{external.Id.Trim()}";
    }

    /// <summary>
    /// Registry entry of a provider channel, looked up by the CANONICAL id persisted on the channel
    /// (not by the external key: famelack nanoids and name-matched M3U channels have external keys
    /// that are not registry ids).
    /// </summary>
    internal static ChannelRegistry ResolveRegistryEntryForKey(
        string channelKey,
        IReadOnlyDictionary<string, string> canonicalByExternalId,
        ChannelRegistryIndex registryIndex)
    {
        if (!canonicalByExternalId.TryGetValue(channelKey, out var canonicalId) ||
            string.IsNullOrWhiteSpace(canonicalId))
            return null;

        return registryIndex.ByCanonicalId.GetValueOrDefault(canonicalId);
    }

    private static Streams NormalizeStream(
        IptvProviders provider, string channelKey, Channels channel, ChannelRegistry reg,
        ExternalStream external, bool requiresIrByRegistry)
    {
        var rawUrl = external.Url.Trim();
        var youTubeEmbed = YouTubeUrls.ToEmbedUrl(rawUrl);
        var url = youTubeEmbed ?? rawUrl;   // official YouTube sources are stored as embed URLs
        var userAgent = external.UserAgent?.Trim() ?? string.Empty;
        var referer = external.Referrer?.Trim() ?? string.Empty;
        var languages = (external.Languages ?? []).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        var quality = StreamQualityParser.Parse(external.Quality);
        var type = external.Type
                   ?? (youTubeEmbed != null ? StreamTypes.YouTube
                       : url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) ? StreamTypes.Hls
                       : StreamTypes.Direct);
        var isAdaptive = external.IsAdaptive || (quality.IsAuto && type == StreamTypes.Hls);

        var labels = external.Labels ?? [];
        var serverProbeUnreliable =
            external.RequiresIranianIp ||
            external.GeoBlocked ||
            requiresIrByRegistry ||
            IsIranianOnlyCdn(url) ||
            labels.Any(l => IsIranLabel(l) || IsGeoLabel(l)) ||
            !StreamTypes.IsStatic(type);   // a HEAD on a page/embed says nothing about playback

        var requiredRegion = ComputeRequiredRegion(
            provider.Kind, external, url, requiresIrByRegistry,
            channel?.Country, reg?.SourceCountry, channel?.CanonicalId ?? channelKey);

        var relayEligible = IsRelayEligible(type, requiredRegion, url);

        var externalId = !string.IsNullOrWhiteSpace(external.StableExternalId)
            ? external.StableExternalId
            : ComputeHash(channelKey, url, userAgent, referer);

        return new Streams
        {
            ProviderPublicKey = provider.PublicKey,
            ChannelId = channel?.ChannelId,
            ExternalId = externalId,
            Name = string.IsNullOrWhiteSpace(external.Title) ? $"{url} ({quality.Normalized})" : external.Title.Trim(),
            StreamUri = url,
            UserAgent = userAgent,
            Referer = referer,
            Type = type,
            Quality = quality.Normalized,
            QualityRank = quality.Rank,
            IsAdaptive = isAdaptive,
            Feed = external.Feed,
            Languages = languages,
            ServerProbeUnreliable = serverProbeUnreliable,
            RequiredRegion = requiredRegion,
            RelayEligible = relayEligible,
            PageUrl = external.PageUrl,
            ResolveMethod = external.ResolveMethod,
            ResolvePattern = external.ResolvePattern,
            ResolveApiUrl = external.ResolveApiUrl,
            ResolveBaseUrl = external.ResolveBaseUrl,
            ResolveHeaders = external.ResolveHeaders,
            ResolveTtlSeconds = external.ResolveTtlSeconds,
            IpBound = external.IpBound,
            PlayerUrl = external.PlayerUrl,
            Embeddable = external.Embeddable,
            IsHealthy = true,   // neutral/optimistic; the health checker demotes dead streams
            DataHash = ComputeHash(url, userAgent, referer, type, quality.Normalized,
                quality.Rank.ToString(), isAdaptive.ToString(), serverProbeUnreliable.ToString(), external.Feed ?? "",
                requiredRegion ?? "", relayEligible.ToString(), external.PageUrl ?? "", external.ResolveMethod ?? "",
                external.ResolvePattern ?? "", external.ResolveApiUrl ?? "", external.ResolveBaseUrl ?? "",
                string.Join(";", (external.ResolveHeaders ?? []).OrderBy(h => h.Key).Select(h => $"{h.Key}={h.Value}")),
                external.ResolveTtlSeconds.ToString(), external.IpBound.ToString(), external.PlayerUrl ?? "",
                external.Embeddable?.ToString() ?? "")
        };
    }

    /// <summary>
    /// The one country a stream plays from, or null:
    /// explicit (resolver) → IR (Iranian CDN, [IR]/Iran label, flags, registry RequiresIranianIp)
    /// → US for Pluto → the source country for Geo-blocked streams (channel country, then registry
    /// SourceCountry, then the ".xx" suffix of the iptv-org id, e.g. ARD.de → DE).
    /// </summary>
    internal static string ComputeRequiredRegion(
        ProviderKind providerKind, ExternalStream external, string url, bool requiresIrByRegistry,
        string channelCountry, string registrySourceCountry, string canonicalId)
    {
        if (!string.IsNullOrWhiteSpace(external.RequiredRegion))
            return NormalizeIso(external.RequiredRegion);

        var labels = external.Labels ?? [];

        if (external.RequiresIranianIp || requiresIrByRegistry || IsIranianOnlyCdn(url) ||
            labels.Any(IsIranLabel))
            return "IR";

        if (providerKind == ProviderKind.Pluto)
            return "US";   // Pluto TV streams are US-only

        if (external.GeoBlocked || labels.Any(IsGeoLabel))
            return SourceCountry(channelCountry, registrySourceCountry, canonicalId);

        return null;
    }

    internal static bool IsRelayEligible(string type, string requiredRegion, string url)
        => requiredRegion == null && type == StreamTypes.Hls && !IsIranianOnlyCdn(url);

    private static string SourceCountry(string channelCountry, string registrySourceCountry, string canonicalId)
    {
        foreach (var candidate in new[] { channelCountry, registrySourceCountry })
            if (candidate?.Trim().Length == 2)
                return NormalizeIso(candidate);

        // iptv-org ids end with the ISO code: "ARD.de", "TRT1.tr", "ProSieben.de@HD".
        var id = canonicalId ?? string.Empty;
        var at = id.IndexOf('@');
        if (at > 0) id = id[..at];
        var dot = id.LastIndexOf('.');
        if (dot > 0 && id.Length - dot - 1 == 2 && id[(dot + 1)..].All(char.IsLetter))
            return NormalizeIso(id[(dot + 1)..]);

        return null;
    }

    private static string NormalizeIso(string country)
    {
        var c = country.Trim().ToUpperInvariant();
        return c == "UK" ? "GB" : c;
    }

    #endregion

    #region Filters

    private static readonly string[] NonPlayableMarkers = ["twitch.tv"];

    /// <summary>
    /// Mass-deactivation guard (§3.8): if a fetch returns 0 items, or fewer than 50% of the
    /// provider's currently active items, do not deactivate anything.
    /// </summary>
    internal static bool ShouldSkipDeactivation(int normalizedCount, int activeExisting)
        => normalizedCount == 0 || (activeExisting > 0 && normalizedCount < activeExisting * 0.5);

    internal static bool IsPlayableUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var u = url.Trim();

        if (u.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase) ||
            u.StartsWith("rtmps://", StringComparison.OrdinalIgnoreCase))
            return false;

        if (u.Contains(".mpd", StringComparison.OrdinalIgnoreCase))  // DASH not supported
            return false;

        if (NonPlayableMarkers.Any(m => u.Contains(m, StringComparison.OrdinalIgnoreCase)))
            return false;

        // Official YouTube live sources are kept as embeds (type "youtube"); a YouTube URL that
        // cannot be expressed as an embed (e.g. an @handle page) is dropped.
        if (YouTubeUrls.IsYouTubeHost(u))
            return YouTubeUrls.ToEmbedUrl(u) != null;

        return Uri.TryCreate(u, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// "[IR]"-style label: the exact token IR (case-insensitive) or a label mentioning Iran.
    /// A substring match on "IR" would also hit unrelated labels such as "IRIB".
    /// </summary>
    internal static bool IsIranLabel(string label)
    {
        var l = label?.Trim();
        return !string.IsNullOrEmpty(l) &&
               (l.Equals("IR", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("Iran", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsGeoLabel(string label)
        => label?.Contains("Geo", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsIngestable(
        ExternalChannel c, HashSet<string> blocked, ChannelRegistryIndex registryIndex)
    {
        if (c.IsNsfw) return false;
        if (!string.IsNullOrWhiteSpace(c.Closed)) return false;
        if (!string.IsNullOrWhiteSpace(c.ReplacedBy)) return false;

        var id = c.Id.Trim();
        if (blocked.Contains(id)) return false;

        // channel@feed: also honour a block on the base channel.
        var at = id.IndexOf('@');
        if (at > 0 && blocked.Contains(id[..at])) return false;

        return true;
    }

    internal static bool IsIranianOnlyCdn(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        return host.Contains("telewebion") ||
               host.EndsWith(".ir") ||
               host.EndsWith(".presstv.ir");
    }

    #endregion

    #region Bulk / logging helpers

    private async Task DeleteChannelsWithStreamsAsync(List<Channels> channels, CancellationToken ct)
    {
        foreach (var chunk in channels.Chunk(1000))
        {
            var channelIds = chunk.Select(c => c.ChannelId).ToList();
            var ids = chunk.Select(c => c.Id).ToList();
            await _streamRepository.DeleteManyAsync(s => channelIds.Contains(s.ChannelId), ct);
            await _channelRepository.DeleteManyAsync(c => ids.Contains(c.Id), ct);
        }

        _logger.LogInformation("Ingest scope Registry: deleted {Count} out-of-scope channels and their streams.",
            channels.Count);
    }

    private static async Task BulkWriteAsync(
        IChannelRepository repo, List<WriteModel<Channels>> writes, CancellationToken ct)
    {
        for (var i = 0; i < writes.Count; i += BulkBatchSize)
            await repo.BulkWriteAsync(writes.GetRange(i, Math.Min(BulkBatchSize, writes.Count - i)), ct);
    }

    private static async Task BulkWriteAsync(
        IStreamRepository repo, List<WriteModel<Streams>> writes, CancellationToken ct)
    {
        for (var i = 0; i < writes.Count; i += BulkBatchSize)
            await repo.BulkWriteAsync(writes.GetRange(i, Math.Min(BulkBatchSize, writes.Count - i)), ct);
    }

    private async Task<IptvSyncResult> CompleteFailedSyncAsync(
        IptvProviders provider, SyncLogs syncLog, string message, CancellationToken cancellationToken)
    {
        syncLog.Status = SyncStatus.Failed;
        syncLog.Message = message;
        syncLog.EndMoment = DateTime.UtcNow;

        await _syncLogRepository.ReplaceOneAsync(syncLog, cancellationToken);
        await MarkProviderSyncAsync(provider, nameof(SyncStatus.Failed), cancellationToken);
        await _eventPublisher.PublishSyncCompletedAsync(syncLog, cancellationToken);

        return MapToResult(provider, syncLog);
    }

    private async Task<IptvSyncResult> CaptureFailedSyncAsync(IptvProviders provider, string message)
    {
        var syncLog = new SyncLogs
        {
            ProviderPublicKey = provider.PublicKey,
            ProviderName = provider.Name,
            Status = SyncStatus.Failed,
            Message = message,
            EndMoment = DateTime.UtcNow
        };

        await _syncLogRepository.InsertOneAsync(syncLog);
        await MarkProviderSyncAsync(provider, nameof(SyncStatus.Failed), CancellationToken.None);
        return MapToResult(provider, syncLog);
    }

    private async Task MarkProviderSyncAsync(
        IptvProviders provider, string status, CancellationToken cancellationToken)
    {
        var update = Builders<IptvProviders>.Update
            .Set(q => q.LastSyncMoment, DateTime.UtcNow)
            .Set(q => q.LastSyncStatus, status);

        await _iptvProviderRepository.FindOneAndUpdateAsync(
            q => q.PublicKey == provider.PublicKey, update, cancellationToken);
    }

    private static string ComputeHash(params string[] values)
    {
        var raw = string.Join("|", values.Select(v => v ?? string.Empty));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static IptvSyncResult MapToResult(IptvProviders provider, SyncLogs syncLog)
        => new()
        {
            ProviderPublicKey = provider.PublicKey,
            ProviderName = provider.Name,
            Status = syncLog.Status.ToString(),
            SyncLogPublicKey = syncLog.PublicKey,
            TotalChannels = syncLog.TotalChannels,
            InsertedChannels = syncLog.InsertedChannels,
            UpdatedChannels = syncLog.UpdatedChannels,
            DeactivatedChannels = syncLog.DeactivatedChannels,
            TotalStreams = syncLog.TotalStreams,
            InsertedStreams = syncLog.InsertedStreams,
            UpdatedStreams = syncLog.UpdatedStreams,
            DeactivatedStreams = syncLog.DeactivatedStreams,
            Message = syncLog.Message
        };

    private class SyncStats
    {
        public int Total { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Deactivated { get; set; }
        public int Deleted { get; set; }
        public bool GuardTripped { get; set; }
    }

    #endregion
}
