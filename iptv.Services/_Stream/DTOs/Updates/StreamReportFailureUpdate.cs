using Utilities.Attributes;

namespace iptv.Services._Stream.DTOs.Updates;

public enum StreamFailureReason
{
    Unknown = 0,
    Timeout = 1,
    Http4xx = 2,
    Http5xx = 3,
    DecoderError = 4
}

public class StreamReportFailureUpdate
{
    [StringInputValidation] public string StreamId { get; set; }

    // Streams the client already tried in this session (so the server never re-offers them).
    public List<string> ExcludeStreamIds { get; set; } = [];

    public StreamFailureReason Reason { get; set; } = StreamFailureReason.Unknown;
}
