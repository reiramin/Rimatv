using Utilities.Attributes;

namespace iptv.Services._Stream.DTOs.Updates;

public class StreamActivateUpdate
{
    [StringInputValidation] public string StreamId { get; set; }
    public required bool ShouldActivate { get; set; }
}
