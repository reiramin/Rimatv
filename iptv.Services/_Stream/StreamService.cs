using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._Channel.Playability;
using iptv.Services._ChannelRegistry.Contracts;
using iptv.Services._IptvNotifier.Contracts;
using iptv.Services._Stream.Contracts;
using iptv.Services._Stream.DTOs.Results;
using iptv.Services._Stream.DTOs.Updates;
using iptv.Services._Stream.Reporting;
using iptv.Services._Stream.Selection;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Utilities.Constants;
using Utilities.Exceptions.Common;
using Utilities.Models.Updates;

namespace iptv.Services._Stream;

public class StreamService(
    IStreamRepository _streamRepository,
    IChannelRepository _channelRepository,
    IIptvProviderRepository _providerRepository,
    IStreamSelector _streamSelector,
    IIptvEventPublisher _eventPublisher,
    IChannelRegistryService _registryService,
    IStreamOutputMapper _outputMapper)
    : IStreamService, RegisterMode.IScopedDependency
{
    public async Task<List<StreamFilteredResult>> GetByChannelAsync(GetGlobalIdUpdate channelId)
    {
        var channel = await _channelRepository.GetByChannelIdAsync(channelId.Id)
                      ?? throw new NotFoundException("Channel not found.");

        var streams = await _streamRepository
            .AsQueryable()
            .Where(q => q.ChannelId == channel.ChannelId && !q.Inactive && !q.AdminDisabled)
            .OrderByDescending(q => q.IsHealthy)
            .ThenByDescending(q => q.QualityRank)
            .ToListAsync();

        return streams.Select(MapToResult).ToList();
    }

    public async Task<List<StreamAdminResult>> GetByChannelForAdminAsync(GetGlobalIdUpdate channelId)
    {
        var channel = await _channelRepository.GetByChannelIdAsync(channelId.Id)
                      ?? throw new NotFoundException("Channel not found.");

        // Admin view: no Inactive / AdminDisabled filter.
        var streams = await _streamRepository
            .AsQueryable()
            .Where(q => q.ChannelId == channel.ChannelId)
            .OrderByDescending(q => q.IsHealthy)
            .ThenByDescending(q => q.QualityRank)
            .ToListAsync();

        // Provider names regardless of the provider's own Inactive flag.
        var providerNames = (await _providerRepository.AsQueryable()
                .Select(p => new { p.PublicKey, p.Name })
                .ToListAsync())
            .GroupBy(p => p.PublicKey)
            .ToDictionary(g => g.Key, g => g.First().Name);

        return streams.Select(s => MapToAdminResult(
                s, s.ProviderPublicKey != null ? providerNames.GetValueOrDefault(s.ProviderPublicKey) : null))
            .ToList();
    }

    public async Task<StreamPlaybackResult> GetPlaybackStreamAsync(GetGlobalIdUpdate channelId)
    {
        var channel = await _channelRepository.GetByChannelIdAsync(channelId.Id)
                      ?? throw new NotFoundException("Channel not found.");

        var now = DateTime.UtcNow;
        var ordered = await SelectCanonicalAsync(channel, now, []);

        if (ordered.Count == 0)
            throw new NotFoundException("No playable stream is currently available for this channel.");

        var winner = ordered[0];
        await StickCurrentStreamAsync(winner);

        return _outputMapper.Fill(new StreamPlaybackResult
        {
            ChannelId = channel.ChannelId,
            CanonicalId = channel.CanonicalId,
            StreamId = winner.StreamId,
            StreamUri = winner.StreamUri,
            UserAgent = winner.UserAgent,
            Referer = winner.Referer,
            Quality = winner.Quality,
            ProviderName = winner.ProviderName,
            Fallbacks = ToFallbacks(ordered.Skip(1).Take(3))
        }, winner);
    }

    public async Task<StreamReportFailureResult> ReportStreamFailureAsync(
        StreamReportFailureUpdate update, ReporterIdentity anonymousReporter = null)
    {
        var stream = await _streamRepository.GetByStreamIdAsync(update.StreamId)
                     ?? throw new NotFoundException("Stream not found.");

        var now = DateTime.UtcNow;
        // Anonymous clients are keyed by SHA256(ip + daily salt) so distinct users are still counted.
        var user = CurrentRequestContext.User?.PublicKey;
        var reporter = user != null
            ? new ReporterIdentity(user, null)
            : anonymousReporter ?? new ReporterIdentity("anonymous", null);

        await RecordFailureAsync(stream, reporter, update.Reason, now);

        var channel = await _channelRepository.GetByChannelIdAsync(stream.ChannelId);

        // Only exclusions that belong to this canonical channel count (never re-offer the reported
        // stream); ids of other channels must not influence the USE_VPN rules.
        var channelStreams = channel != null ? await LoadCanonicalStreamsAsync(channel) : [stream];
        var excludedDocs = ReportFailureOutcome.ScopeExcluded(update.ExcludeStreamIds, channelStreams, stream);
        var exclude = excludedDocs.Select(s => s.StreamId).ToHashSet(StringComparer.Ordinal);

        var ordered = channel != null
            ? await OrderCanonicalAsync(channel, channelStreams, now, exclude)
            : [];

        var replacement = ordered.FirstOrDefault();

        // Keep publishing the SignalR event (reported stream + replacement).
        var replacementDoc = replacement == null
            ? null
            : new Streams { StreamId = replacement.StreamId, ChannelId = replacement.ChannelId, StreamUri = replacement.StreamUri };
        await _eventPublisher.PublishStreamHealthChangedAsync(stream, replacementDoc);

        if (replacement == null)
        {
            var decision = ReportFailureOutcome.Decide(stream, excludedDocs, update.Reason, now);

            var result = new StreamReportFailureResult
            {
                Found = false,
                ErrorCode = ReportFailureOutcome.NoAlternativeStream,
                ChannelId = channel?.ChannelId ?? stream.ChannelId,
                CanonicalId = channel?.CanonicalId
            };

            if (decision.UseVpn)
            {
                var (en, fa) = ChannelStatusRules.VpnMessages(decision.RequiredRegions);
                result.ErrorCode = ChannelStatusRules.UseVpn;
                result.RequiredRegions = decision.RequiredRegions;
                result.Message = en;
                result.MessageFa = fa;
                result.VpnHelpUrl = string.IsNullOrWhiteSpace(_outputMapper.VpnHelpUrl) ? null : _outputMapper.VpnHelpUrl;
            }

            return result;
        }

        await StickCurrentStreamAsync(replacement);

        return _outputMapper.Fill(new StreamReportFailureResult
        {
            Found = true,
            ChannelId = replacement.ChannelId,
            CanonicalId = replacement.CanonicalId,
            StreamId = replacement.StreamId,
            StreamUri = replacement.StreamUri,
            UserAgent = replacement.UserAgent,
            Referer = replacement.Referer,
            Quality = replacement.Quality,
            ProviderName = replacement.ProviderName,
            Fallbacks = ToFallbacks(ordered.Skip(1).Take(3))
        }, replacement);
    }

    public async Task<StreamFilteredResult> ActivateAsync(StreamActivateUpdate update)
    {
        var stream = await _streamRepository.GetByStreamIdAsync(update.StreamId)
                     ?? throw new NotFoundException("Stream not found.");

        // Admin intent lives in AdminDisabled so sync cannot silently re-enable it.
        var updateDef = Builders<Streams>.Update
            .Set(q => q.AdminDisabled, !update.ShouldActivate)
            .Set(q => q.ModifiedMoment, DateTime.UtcNow);

        await _streamRepository.FindOneAndUpdateAsync(q => q.Id == stream.Id, updateDef);

        stream.AdminDisabled = !update.ShouldActivate;
        _Channel.ChannelService.InvalidateSharedLiteCache();

        return MapToResult(stream);
    }

    public async Task<string> DeleteAsync(StreamDeleteUpdate update)
    {
        var stream = await _streamRepository.GetByStreamIdAsync(update.StreamId)
                     ?? throw new NotFoundException("Stream not found.");

        await _streamRepository.DeleteOneAsync(q => q.Id == stream.Id);
        _Channel.ChannelService.InvalidateSharedLiteCache();

        return stream.StreamId;
    }

    #region Canonical, cross-provider selection

    private async Task<IReadOnlyList<StreamCandidate>> SelectCanonicalAsync(
        Channels channel, DateTime now, HashSet<string> exclude)
        => await OrderCanonicalAsync(channel, await LoadCanonicalStreamsAsync(channel), now, exclude);

    // Active streams of all provider-channels sharing this canonical id (cross-provider).
    private async Task<List<Streams>> LoadCanonicalStreamsAsync(Channels channel)
    {
        var canonical = string.IsNullOrWhiteSpace(channel.CanonicalId) ? null : channel.CanonicalId;

        var channelIds = canonical == null
            ? [channel.ChannelId]
            : await _channelRepository.AsQueryable()
                .Where(c => c.CanonicalId == canonical && !c.Inactive && !c.AdminDisabled)
                .Select(c => c.ChannelId)
                .ToListAsync();

        if (channelIds.Count == 0)
            channelIds = [channel.ChannelId];

        return await _streamRepository.AsQueryable()
            .Where(s => channelIds.Contains(s.ChannelId) && !s.Inactive && !s.AdminDisabled)
            .ToListAsync();
    }

    private async Task<IReadOnlyList<StreamCandidate>> OrderCanonicalAsync(
        Channels channel, List<Streams> streams, DateTime now, HashSet<string> exclude)
    {
        var canonical = string.IsNullOrWhiteSpace(channel.CanonicalId) ? null : channel.CanonicalId;

        var providers = await LoadProvidersAsync();

        // Streams of an inactive (or deleted) provider are skipped by the factory.
        var candidates = StreamCandidateFactory.FromActiveProviders(
            streams, _ => canonical ?? channel.ChannelId, providers, now);

        var index = await _registryService.GetIndexAsync();
        var curatedCountry = canonical != null && index.ByCanonicalId.TryGetValue(canonical, out var reg)
            ? reg.CuratedCountry
            : null;

        var context = new StreamSelectionContext
        {
            CuratedCountry = curatedCountry,
            CurrentStreamId = channel.CurrentStreamId,
            Now = now,
            ExcludeStreamIds = exclude ?? new HashSet<string>(StringComparer.Ordinal)
        };

        return _streamSelector.Order(candidates, context);
    }

    private async Task<Dictionary<string, ProviderInfo>> LoadProvidersAsync()
        => (await _providerRepository.AsQueryable()
                .Where(p => !p.Inactive)
                .Select(p => new { p.PublicKey, p.Name, p.Priority })
                .ToListAsync())
            .GroupBy(p => p.PublicKey)
            .ToDictionary(g => g.Key, g => new ProviderInfo(g.First().Name, g.First().Priority));

    private async Task StickCurrentStreamAsync(StreamCandidate winner)
    {
        var owner = await _channelRepository.GetByChannelIdAsync(winner.ChannelId);
        if (owner == null || owner.CurrentStreamId == winner.StreamId)
            return;

        var update = Builders<Channels>.Update
            .Set(q => q.CurrentStreamId, winner.StreamId)
            .Set(q => q.ModifiedMoment, DateTime.UtcNow);

        await _channelRepository.FindOneAndUpdateAsync(q => q.Id == owner.Id, update);
    }

    private async Task RecordFailureAsync(
        Streams stream, ReporterIdentity reporter, StreamFailureReason reason, DateTime now)
    {
        // Prune reports older than the decay window, then append this one.
        var recent = (stream.RecentClientFailures ?? [])
            .Where(r => r.Moment >= now - StreamFailureConstants.DecayWindow)
            .ToList();

        recent.Add(new ClientFailureReport
        {
            UserPublicKey = reporter.Key,
            PreviousUserPublicKey = reporter.PreviousKey,
            Moment = now,
            Reason = reason.ToString()
        });

        var distinctInWindow = ReporterCounting.Distinct(
            recent.Where(r => r.Moment >= now - StreamFailureConstants.ReportWindow));

        var updateDef = Builders<Streams>.Update
            .Inc(q => q.ClientFailureCount, 1)
            .Set(q => q.LastClientFailureMoment, now)
            .Set(q => q.RecentClientFailures, recent)
            .Set(q => q.ModifiedMoment, now);

        // >=3 distinct users within the report window => exclude until 30 min after this report.
        if (distinctInWindow >= StreamFailureConstants.DistinctUserThreshold)
            updateDef = updateDef.Set(q => q.ClientFailingUntil, now + StreamFailureConstants.DecayWindow);

        await _streamRepository.FindOneAndUpdateAsync(q => q.Id == stream.Id, updateDef);
    }

    private List<PlaybackFallback> ToFallbacks(IEnumerable<StreamCandidate> candidates)
        => candidates.Select(c => _outputMapper.Fill(new PlaybackFallback
        {
            StreamId = c.StreamId,
            StreamUri = c.StreamUri,
            UserAgent = c.UserAgent,
            Referer = c.Referer,
            Quality = c.Quality,
            ProviderName = c.ProviderName
        }, c)).ToList();

    #endregion

    #region Helpers

    internal static StreamFilteredResult MapToResult(Streams stream)
        => new()
        {
            StreamId = stream.StreamId,
            ChannelId = stream.ChannelId,
            Name = stream.Name,
            StreamUri = stream.StreamUri,
            Type = stream.Type,
            Quality = stream.Quality,
            IsHealthy = stream.IsHealthy,
            Inactive = stream.Inactive,
            AdminDisabled = stream.AdminDisabled,
            LastCheckedMoment = stream.LastCheckedMoment
        };

    internal static StreamAdminResult MapToAdminResult(Streams stream, string providerName)
        => new()
        {
            StreamId = stream.StreamId,
            ChannelId = stream.ChannelId,
            Name = stream.Name,
            StreamUri = stream.StreamUri,
            Type = stream.Type,
            Quality = stream.Quality,
            IsHealthy = stream.IsHealthy,
            Inactive = stream.Inactive,
            AdminDisabled = stream.AdminDisabled,
            LastCheckedMoment = stream.LastCheckedMoment,
            ProviderPublicKey = stream.ProviderPublicKey,
            ProviderName = providerName,
            ProbeStatus = stream.ProbeStatus,
            ProbeMoment = stream.ProbeMoment,
            RequiredRegion = stream.RequiredRegion,
            WebCompatible = stream.WebCompatible
        };

    #endregion
}
