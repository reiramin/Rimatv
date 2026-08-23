namespace iptv.Services._IptvProvider.DTOs.Results;

public class IptvProviderFilteredResult
{
    public string PublicKey { get; set; }

    public string Name { get; set; }
    public string BaseUrl { get; set; }
    public string ChannelsEndpoint { get; set; }
    public string StreamsEndpoint { get; set; }
    public string LogosEndpoint { get; set; }

    public bool Inactive { get; set; }

    public DateTime? LastSyncMoment { get; set; }
    public string LastSyncStatus { get; set; }

    public string CreatedByInfo { get; set; }
    public DateTime CreatedMoment { get; set; }

    public string ModifiedByInfo { get; set; }
    public DateTime? ModifiedMoment { get; set; }
}