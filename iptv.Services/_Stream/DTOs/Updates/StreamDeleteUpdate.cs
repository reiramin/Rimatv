using Utilities.Attributes;

namespace iptv.Services._Stream.DTOs.Updates;

public class StreamDeleteUpdate
{
    [StringInputValidation] public string StreamId { get; set; }
}