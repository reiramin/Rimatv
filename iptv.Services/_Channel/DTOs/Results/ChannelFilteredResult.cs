using System.Text.Json.Serialization;

namespace iptv.Services._Channel.DTOs.Results;

public class ChannelFilteredResult
{
    public string ChannelId { get; set; }
    public string ProviderPublicKey { get; set; }
    public string ExternalId { get; set; }
    public string Name { get; set; }
    public string ImageUri { get; set; }
    public string Country { get; set; }
    public string Category { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string CurrentStreamId { get; set; }
    public bool Inactive { get; set; }
    public DateTime CreatedMoment { get; set; }
    public DateTime? ModifiedMoment { get; set; }
}

public class ChannelBasicResult
{
    public string ChannelId { get; set; }
    public string Name { get; set; }
    public string ImageUri { get; set; }
    public string Country { get; set; }
    public string Category { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string CurrentStreamId { get; set; }
}

[JsonConverter(typeof(ChannelWithStreamResultJsonConverter))]
public class ChannelWithStreamResult
{
    public string ChannelId { get; set; }
    public string Name { get; set; }
    public string ImageUri { get; set; }
    public string Country { get; set; }
    public string Category { get; set; }
    public string? CurrentStreamUrl { get; set; }
    
    public bool Inactive { get; set; }

}
