using Utilities.Attributes;

namespace iptv.Services._Stream.DTOs.Updates;

public class StreamReportFailureUpdate
{
    [StringInputValidation] public string StreamId { get; set; }
}