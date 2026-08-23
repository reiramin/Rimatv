using Utilities.Attributes;

namespace iptv.Services._Channel.DTOs.Updates;

public class ChannelActivateUpdate
{
    [StringInputValidation]public string ChannelId { get; set; }
    public required bool ShouldActivate { get; set; }
}
