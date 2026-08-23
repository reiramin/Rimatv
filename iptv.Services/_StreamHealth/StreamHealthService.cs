using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._IptvNotifier.Contracts;
using iptv.Services._StreamHealth.Contracts;
using iptv.Services._StreamHealth.DTOs.Results;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Net.Http;
using Utilities.Constants;

namespace iptv.Services._StreamHealth;

public class StreamHealthService(
    IStreamRepository _streamRepository,
    IChannelRepository _channelRepository,
    IHttpClientFactory _httpClientFactory,
    IIptvEventPublisher _eventPublisher,
    ILogger<StreamHealthService> _logger)
    : IStreamHealthService, RegisterMode.IScopedDependency
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    public async Task<StreamHealthCheckResult> CheckStaleStreamsAsync(int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var staleStreams = await _streamRepository.GetStaleStreamsAsync(
            DateTime.UtcNow.AddMinutes(-5), limit, cancellationToken);

        return await CheckStreamsAsync(staleStreams, cancellationToken);
    }

    public async Task<StreamHealthCheckResult> CheckChannelStreamsAsync(string channelId,
        CancellationToken cancellationToken = default)
    {
        var streams = await _streamRepository.GetByChannelAsync(channelId, cancellationToken);
        var activeStreams = streams.Where(q => !q.Inactive).ToList();

        return await CheckStreamsAsync(activeStreams, cancellationToken);
    }

    public async Task<bool> CheckStreamAsync(string streamId,
        CancellationToken cancellationToken = default)
    {
        var stream = await _streamRepository.GetByStreamIdAsync(streamId, cancellationToken);
        if (stream == null)
            return false;

        var result = await CheckStreamsAsync([stream], cancellationToken);

        return result.HealthyCount > 0;
    }

    #region Health Check Internals

    private async Task<StreamHealthCheckResult> CheckStreamsAsync(
        List<Streams> streams,
        CancellationToken cancellationToken)
    {
        var result = new StreamHealthCheckResult();

        foreach (var stream in streams.Where(q => !q.Inactive))
        {
            result.CheckedCount++;

            var isHealthy = await ProbeAsync(stream, cancellationToken);

            if (isHealthy) result.HealthyCount++;
            else result.UnhealthyCount++;

            if (isHealthy == stream.IsHealthy &&
                stream.LastCheckedMoment != null)
            {
                await _streamRepository.FindOneAndUpdateAsync(
                    q => q.Id == stream.Id,
                    Builders<Streams>.Update.Set(q => q.LastCheckedMoment, DateTime.UtcNow),
                    cancellationToken);
                continue;
            }

            var update = Builders<Streams>.Update
                .Set(q => q.IsHealthy, isHealthy)
                .Set(q => q.LastCheckedMoment, DateTime.UtcNow)
                .Set(q => q.ModifiedMoment, DateTime.UtcNow);

            await _streamRepository.FindOneAndUpdateAsync(
                q => q.Id == stream.Id, update, cancellationToken);

            if (!isHealthy)
            {
                stream.IsHealthy = false;
                stream.LastCheckedMoment = DateTime.UtcNow;

                var replacement = await SelectReplacementStreamAsync(stream, cancellationToken);

                await ApplyStreamFallbackAsync(stream, replacement, cancellationToken);

                await _eventPublisher.PublishStreamHealthChangedAsync(
                    stream, replacement, cancellationToken);

                continue;
            }

            stream.IsHealthy = true;
            stream.LastCheckedMoment = DateTime.UtcNow;

            await _eventPublisher.PublishStreamHealthChangedAsync(
                stream, null, cancellationToken);
        }

        return result;
    }

    private async Task<bool> ProbeAsync(Streams stream, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient("IptvStreamCheck");
            httpClient.Timeout = ProbeTimeout;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ProbeTimeout);

            var request = new HttpRequestMessage(HttpMethod.Head, stream.StreamUri);
            ApplyStreamHeaders(request, stream);

            using var response = await httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed ||
                response.StatusCode == System.Net.HttpStatusCode.NotImplemented)
            {
                var getRequest = new HttpRequestMessage(HttpMethod.Get, stream.StreamUri);
                getRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                ApplyStreamHeaders(getRequest, stream);

                using var getResponse = await httpClient.SendAsync(getRequest,
                    HttpCompletionOption.ResponseHeadersRead, cts.Token);

                return getResponse.IsSuccessStatusCode;
            }

            return response.IsSuccessStatusCode ||
                   (int)response.StatusCode >= 300 && (int)response.StatusCode < 400;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Stream probe failed for {StreamUri}.", stream.StreamUri);
            return false;
        }
    }

    private static void ApplyStreamHeaders(HttpRequestMessage request, Streams stream)
    {
        if (!string.IsNullOrWhiteSpace(stream.UserAgent))
            request.Headers.UserAgent.ParseAdd(stream.UserAgent);

        if (!string.IsNullOrWhiteSpace(stream.Referer))
            request.Headers.Referrer = new Uri(stream.Referer);
    }

    private async Task<Streams> SelectReplacementStreamAsync(
        Streams failedStream,
        CancellationToken cancellationToken)
        => await _streamRepository.AsQueryable()
            .Where(q => q.ChannelId == failedStream.ChannelId &&
                        q.StreamId != failedStream.StreamId &&
                        !q.Inactive &&
                        q.IsHealthy)
            .OrderByDescending(q => q.QualityRank)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task ApplyStreamFallbackAsync(
        Streams failedStream,
        Streams replacement,
        CancellationToken cancellationToken)
    {
        var channel = await _channelRepository
            .GetByChannelIdAsync(failedStream.ChannelId, cancellationToken);

        if (channel == null)
            return;

        if (channel.CurrentStreamId != failedStream.StreamId && !string.IsNullOrEmpty(channel.CurrentStreamId))
            return;

        var update = Builders<Channels>.Update
            .Set(q => q.CurrentStreamId, replacement?.StreamId)
            .Set(q => q.ModifiedMoment, DateTime.UtcNow);

        await _channelRepository.FindOneAndUpdateAsync(
            q => q.Id == channel.Id, update, cancellationToken);
    }

    #endregion
}
