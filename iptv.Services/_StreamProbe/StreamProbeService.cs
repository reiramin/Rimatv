using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._Channel;
using iptv.Services._StreamProbe.DTOs;
using MongoDB.Driver;
using Utilities.Constants;
using Utilities.Exceptions.Common;

namespace iptv.Services._StreamProbe.Contracts
{
    public interface IStreamProbeService
    {
        Task<ProbeReportResult> ApplyAsync(List<ProbeResultItem> items, CancellationToken cancellationToken = default);
    }
}

namespace iptv.Services._StreamProbe
{
    using Contracts;

    /// <summary>
    /// Applies deep-probe results (§4.3) posted by the GitHub Actions validator. One BulkWriteAsync
    /// that sets ONLY the probe fields and WebCompatible, then invalidates the lite cache.
    /// </summary>
    public class StreamProbeService(IStreamRepository _streamRepository)
        : IStreamProbeService, RegisterMode.IScopedDependency
    {
        public const int MaxItems = 5000;

        private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
        {
            StreamProbeStatus.Ok, StreamProbeStatus.Dead, StreamProbeStatus.Region, StreamProbeStatus.Unverified
        };

        public async Task<ProbeReportResult> ApplyAsync(List<ProbeResultItem> items, CancellationToken cancellationToken = default)
        {
            if (items == null)
                throw new BadRequestException("A JSON array of probe results is required.");
            if (items.Count > MaxItems)
                throw new BadRequestException($"At most {MaxItems} items per call.");

            var now = DateTime.UtcNow;
            var writes = BuildWrites(items, now, out var skipped);

            if (writes.Count > 0)
            {
                await _streamRepository.BulkWriteAsync(writes, cancellationToken);
                ChannelService.InvalidateSharedLiteCache();
            }

            return new ProbeReportResult { Received = items.Count, Applied = writes.Count, Skipped = skipped };
        }

        internal static List<WriteModel<Streams>> BuildWrites(List<ProbeResultItem> items, DateTime now, out int skipped)
        {
            skipped = 0;
            var writes = new List<WriteModel<Streams>>(items.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in items)
            {
                var status = item.Status?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(item.StreamId) || status == null || !Statuses.Contains(status) ||
                    !seen.Add(item.StreamId))
                {
                    skipped++;
                    continue;
                }

                // Clamp the moment: never in the future, never older than a day (clock skew / replays).
                var moment = item.Moment?.ToUniversalTime() ?? now;
                if (moment > now || moment < now.AddDays(-1))
                    moment = now;

                var update = Builders<Streams>.Update
                    .Set(s => s.ProbeStatus, status)
                    .Set(s => s.ProbeMoment, moment)
                    .Set(s => s.ProbeRegionHint, string.IsNullOrWhiteSpace(item.RegionHint) ? null : item.RegionHint.Trim().ToUpperInvariant());

                if (item.WebCompatible.HasValue)
                    update = update.Set(s => s.WebCompatible, item.WebCompatible.Value);

                writes.Add(new UpdateOneModel<Streams>(
                    Builders<Streams>.Filter.Eq(s => s.StreamId, item.StreamId), update));
            }

            return writes;
        }
    }
}
