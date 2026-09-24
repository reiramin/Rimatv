using iptv.Services._Stream.DTOs.Results;
using iptv.Services._Stream.DTOs.Updates;
using Utilities.Models.Updates;
using Utilities.MongoDatabase.Filter;

namespace iptv.Services._Stream.Contracts;

public interface IStreamService
{
    Task<List<StreamFilteredResult>> GetByChannelAsync(GetGlobalIdUpdate channelId);

    Task<StreamPlaybackResult> GetPlaybackStreamAsync(GetGlobalIdUpdate channelId);

    Task<StreamReportFailureResult> ReportStreamFailureAsync(
        StreamReportFailureUpdate update, string anonymousReporterKey = null);

    Task<StreamFilteredResult> ActivateAsync(StreamActivateUpdate update);

    Task<string> DeleteAsync(StreamDeleteUpdate update);
}
