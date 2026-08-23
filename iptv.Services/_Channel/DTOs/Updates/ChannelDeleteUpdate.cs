using Utilities.Attributes;

namespace iptv.Services._Channel.DTOs.Updates;

public class ChannelDeleteUpdate
{
    [StringInputValidation] public string ChannelId { get; set; }
}