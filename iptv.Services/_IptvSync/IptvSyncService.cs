using System.Security.Cryptography;
using System.Text;
using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._IptvNotifier.Contracts;
using iptv.Services._IptvProvider.Contracts;
using iptv.Services._IptvProvider.DTOs.Results;
using iptv.Services._IptvSync.Contracts;
using iptv.Services._IptvSync.DTOs.Results;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Utilities.Constants;

namespace iptv.Services._IptvSync;

public class IptvSyncService(
    IIptvProviderRepository _iptvProviderRepository,
    IIptvProviderService _iptvProviderService,
    IChannelRepository _channelRepository,
    IStreamRepository _streamRepository,
    ISyncLogRepository _syncLogRepository,
    IIptvEventPublisher _eventPublisher,
    ILogger<IptvSyncService> _logger)
    : IIptvSyncService, RegisterMode.IScopedDependency
{
    public async Task<List<IptvSyncResult>> SyncAllProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        var providers = await _iptvProviderService.GetActiveProvidersAsync(cancellationToken);

        var results = new List<IptvSyncResult>();
        foreach (var provider in providers)
        {
            try
            {
                results.Add(await SyncProviderInternalAsync(provider, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "IPTV sync for provider {ProviderName} failed unexpectedly.",
                    provider.Name);

                results.Add(await CaptureFailedSyncAsync(provider, ex.Message));
            }
        }

        return results;
    }

    public async Task<IptvSyncResult> SyncProviderAsync(string providerPublicKey,
        CancellationToken cancellationToken = default)
    {
        var provider = await _iptvProviderRepository.GetByPublicKeyAsync(providerPublicKey);

        return await SyncProviderInternalAsync(provider, cancellationToken);
    }

    #region Sync Internals

    private async Task<IptvSyncResult> SyncProviderInternalAsync(
        IptvProviders provider,
        CancellationToken cancellationToken)
    {
        var syncLog = new SyncLogs
        {
            ProviderPublicKey = provider.PublicKey,
            ProviderName = provider.Name,
            Status = SyncStatus.Running
        };
        await _syncLogRepository.InsertOneAsync(syncLog, cancellationToken);

        List<ExternalChannel> externalChannels;
        try
        {
            externalChannels = await _iptvProviderService
                .FetchChannelsAsync(provider, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to fetch channels from provider {ProviderName}. Existing data is left untouched.",
                provider.Name);

            return await CompleteFailedSyncAsync(provider, syncLog,
                $"Channel fetch failed: {ex.Message}", cancellationToken);
        }

        List<ExternalStream> externalStreams = [];
        var streamsFetchSucceeded = true;
        if (!string.IsNullOrWhiteSpace(provider.StreamsEndpoint))
        {
            try
            {
                externalStreams = await _iptvProviderService
                    .FetchStreamsAsync(provider, cancellationToken);
            }
            catch (Exception ex)
            {
                streamsFetchSucceeded = false;
                _logger.LogWarning(ex,
                    "Failed to fetch streams from provider {ProviderName}. Streams are left untouched.",
                    provider.Name);
                syncLog.Message = $"Stream fetch failed: {ex.Message}";
            }
        }

        var logosByExternalChannelId = await FetchLogosIfNeededAsync(provider, externalChannels,
            cancellationToken);

        var channelStats = await SyncChannelsAsync(provider, externalChannels,
            logosByExternalChannelId, cancellationToken);

        var localChannelIdByExternalId = (await _channelRepository
                .AsQueryable()
                .Where(q => q.ProviderPublicKey == provider.PublicKey)
                .Select(q => new { q.ExternalId, q.ChannelId })
                .ToListAsync(cancellationToken))
            .ToDictionary(q => q.ExternalId, q => q.ChannelId);

        var streamStats = streamsFetchSucceeded
            ? await SyncStreamsAsync(provider, externalStreams, localChannelIdByExternalId,
                cancellationToken)
            : new SyncStats();

        syncLog.Status = streamsFetchSucceeded ? SyncStatus.Success : SyncStatus.PartialSuccess;
        if (syncLog.Status != SyncStatus.Success && string.IsNullOrEmpty(syncLog.Message))
            syncLog.Message = "Streams endpoint failed; channels synced successfully.";
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

        return MapToResult(provider, syncLog);
    }

    private async Task<Dictionary<string, string>> FetchLogosIfNeededAsync(
        IptvProviders provider,
        List<ExternalChannel> externalChannels,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider.LogosEndpoint) ||
            externalChannels.All(q => !string.IsNullOrWhiteSpace(q.Logo)))
            return [];

        try
        {
            var logos = await _iptvProviderService.FetchLogosAsync(provider, cancellationToken);

            return logos
                .Where(q => !string.IsNullOrWhiteSpace(q.Channel) &&
                            !string.IsNullOrWhiteSpace(q.Logo))
                .GroupBy(q => q.Channel)
                .ToDictionary(q => q.Key, q => q.First().Logo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to fetch logos from provider {ProviderName}. Continuing without logos.",
                provider.Name);
            return [];
        }
    }

    private async Task<SyncStats> SyncChannelsAsync(
        IptvProviders provider,
        List<ExternalChannel> externalChannels,
        Dictionary<string, string> logosByExternalChannelId,
        CancellationToken cancellationToken)
    {
        var stats = new SyncStats();

        var normalizedChannels = externalChannels
            .Where(q => !string.IsNullOrWhiteSpace(q.Id) && !string.IsNullOrWhiteSpace(q.Name))
            .GroupBy(q => q.Id)
            .Select(q => q.First())
            .Select(q => NormalizeChannel(q, logosByExternalChannelId))
            .ToList();
        stats.Total = normalizedChannels.Count;

        var normalizedByExternalId = normalizedChannels.ToDictionary(q => q.ExternalId);

        var existingChannels = await _channelRepository
            .GetByProviderAsync(provider.PublicKey, cancellationToken);

        var existingByExternalId = existingChannels.ToDictionary(q => q.ExternalId);

        var toInsert = new List<Channels>();
        foreach (var normalized in normalizedChannels)
        {
            if (!existingByExternalId.TryGetValue(normalized.ExternalId, out var existing))
            {
                normalized.ProviderPublicKey = provider.PublicKey;
                toInsert.Add(normalized);
                stats.Inserted++;
                continue;
            }

            if (existing.DataHash == normalized.DataHash && !existing.Inactive)
                continue;

            var update = Builders<Channels>.Update
                .Set(q => q.Name, normalized.Name)
                .Set(q => q.ImageUri, normalized.ImageUri)
                .Set(q => q.Country, normalized.Country)
                .Set(q => q.Category, normalized.Category)
                .Set(q => q.DataHash, normalized.DataHash)
                .Set(q => q.Inactive, false)
                .Set(q => q.ModifiedMoment, DateTime.UtcNow);

            await _channelRepository.FindOneAndUpdateAsync(
                q => q.Id == existing.Id, update, cancellationToken);
            stats.Updated++;
        }

        if (toInsert.Count > 0)
            await _channelRepository.InsertManyAsync(toInsert, cancellationToken);

        var removedIds = existingChannels
            .Where(q => !q.Inactive && !normalizedByExternalId.ContainsKey(q.ExternalId))
            .Select(q => q.Id)
            .ToList();

        if (removedIds.Count > 0)
        {
            var deactivate = Builders<Channels>.Update
                .Set(q => q.Inactive, true)
                .Set(q => q.ModifiedMoment, DateTime.UtcNow);

            await _channelRepository.UpdateManyAsync(
                q => removedIds.Contains(q.Id), deactivate, cancellationToken);
            stats.Deactivated = removedIds.Count;
        }

        return stats;
    }

    private async Task<SyncStats> SyncStreamsAsync(
        IptvProviders provider,
        List<ExternalStream> externalStreams,
        Dictionary<string, string> localChannelIdByExternalId,
        CancellationToken cancellationToken)
    {
        var stats = new SyncStats();

        var normalizedStreams = externalStreams
            .Where(q => !string.IsNullOrWhiteSpace(q.Url) &&
                        !string.IsNullOrWhiteSpace(q.Channel) &&
                        localChannelIdByExternalId.ContainsKey(q.Channel))
            .GroupBy(q => $"{q.Url}|{q.UserAgent ?? string.Empty}|{q.Referrer ?? string.Empty}")
            .Select(q => q.First())
            .Select(q => (Stream: NormalizeStream(q), ExternalChannelId: q.Channel.Trim()))
            .ToList();
        stats.Total = normalizedStreams.Count;

        var normalizedByExternalId = normalizedStreams
            .ToDictionary(q => q.Stream.ExternalId);
        var existingStreams = await _streamRepository
            .GetByProviderAsync(provider.PublicKey, cancellationToken);

        var existingByExternalId = existingStreams.ToDictionary(q => q.ExternalId);

        var toInsert = new List<Streams>();
        foreach (var (normalized, externalChannelId) in normalizedStreams)
        {
            if (!existingByExternalId.TryGetValue(normalized.ExternalId, out var existing))
            {
                normalized.ProviderPublicKey = provider.PublicKey;
                normalized.ChannelId = localChannelIdByExternalId[externalChannelId];
                toInsert.Add(normalized);
                stats.Inserted++;
                continue;
            }

            if (existing.DataHash == normalized.DataHash && !existing.Inactive)
                continue;

            var update = Builders<Streams>.Update
                .Set(q => q.Name, normalized.Name)
                .Set(q => q.ChannelId, localChannelIdByExternalId[externalChannelId])
                .Set(q => q.StreamUri, normalized.StreamUri)
                .Set(q => q.UserAgent, normalized.UserAgent)
                .Set(q => q.Referer, normalized.Referer)
                .Set(q => q.Type, normalized.Type)
                .Set(q => q.Quality, normalized.Quality)
                .Set(q => q.QualityRank, normalized.QualityRank)
                .Set(q => q.DataHash, normalized.DataHash)
                .Set(q => q.Inactive, false)
                .Set(q => q.ModifiedMoment, DateTime.UtcNow);

            await _streamRepository.FindOneAndUpdateAsync(
                q => q.Id == existing.Id, update, cancellationToken);
            stats.Updated++;
        }

        if (toInsert.Count > 0)
            await _streamRepository.InsertManyAsync(toInsert, cancellationToken);

        var removedIds = existingStreams
            .Where(q => !q.Inactive && !normalizedByExternalId.ContainsKey(q.ExternalId))
            .Select(q => q.Id)
            .ToList();

        if (removedIds.Count > 0)
        {
            var deactivate = Builders<Streams>.Update
                .Set(q => q.Inactive, true)
                .Set(q => q.ModifiedMoment, DateTime.UtcNow);

            await _streamRepository.UpdateManyAsync(
                q => removedIds.Contains(q.Id), deactivate, cancellationToken);
            stats.Deactivated = removedIds.Count;
        }

        return stats;
    }

    private async Task<IptvSyncResult> CompleteFailedSyncAsync(
        IptvProviders provider,
        SyncLogs syncLog,
        string message,
        CancellationToken cancellationToken)
    {
        syncLog.Status = SyncStatus.Failed;
        syncLog.Message = message;
        syncLog.EndMoment = DateTime.UtcNow;
        await _syncLogRepository.ReplaceOneAsync(syncLog, cancellationToken);

        await MarkProviderSyncAsync(provider, nameof(SyncStatus.Failed), cancellationToken);

        await _eventPublisher.PublishSyncCompletedAsync(syncLog, cancellationToken);

        return MapToResult(provider, syncLog);
    }

    private async Task<IptvSyncResult> CaptureFailedSyncAsync(
        IptvProviders provider,
        string message)
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
        IptvProviders provider,
        string status,
        CancellationToken cancellationToken)
    {
        var update = Builders<IptvProviders>.Update
            .Set(q => q.LastSyncMoment, DateTime.UtcNow)
            .Set(q => q.LastSyncStatus, status);

        await _iptvProviderRepository.FindOneAndUpdateAsync(
            q => q.PublicKey == provider.PublicKey, update, cancellationToken);
    }

    #endregion

    #region Normalization & Hashing

    private static Channels NormalizeChannel(
        ExternalChannel external,
        Dictionary<string, string> logosByExternalChannelId)
    {
        var name = external.Name.Trim();
        var country = (external.Country ?? string.Empty).Trim().ToUpperInvariant();
        var category = external.Categories?.FirstOrDefault()?.Trim();
        var imageUri = !string.IsNullOrWhiteSpace(external.Logo)
            ? external.Logo.Trim()
            : logosByExternalChannelId.GetValueOrDefault(external.Id)?.Trim();

        return new Channels
        {
            ExternalId = external.Id.Trim(),
            Name = name,
            Country = country,
            Category = category,
            ImageUri = imageUri,
            DataHash = ComputeHash(name, country, category, imageUri)
        };
    }

    private static Streams NormalizeStream(ExternalStream external)
    {
        var url = external.Url.Trim();
        var userAgent = external.UserAgent?.Trim();
        var referer = external.Referrer?.Trim();

        return new Streams
        {
            ExternalId = ComputeHash(url, userAgent, referer),
            Name = url,
            StreamUri = url,
            UserAgent = userAgent,
            Referer = referer,
            Quality = external.Quality,
            QualityRank = ParseQualityRank(external.Quality),
            Type = url.Contains(".m3u8") ? "hls" : "direct",
            IsHealthy = true,
            DataHash = ComputeHash(url, userAgent, referer,
                url.Contains(".m3u8") ? "hls" : "direct",
                external.Quality?.ToString())
        };
    }

    private static int ParseQualityRank(string quality)
    {
        if (string.IsNullOrWhiteSpace(quality))
            return 0;

        var digits = new string(quality.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var rank))
            return rank;

        return quality.Trim().ToLowerInvariant() switch
        {
            "4k" or "uhd" => 2160,
            "fhd" => 1080,
            "hd" => 720,
            "sd" => 480,
            _ => 0
        };
    }

    private static string ComputeHash(params string[] values)
    {
        var raw = string.Join("|", values.Select(q => q ?? string.Empty));

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));

        return Convert.ToHexString(hashBytes);
    }

    #endregion

    #region Helpers

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
    }

    #endregion
}