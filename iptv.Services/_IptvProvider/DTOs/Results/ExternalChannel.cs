namespace iptv.Services._IptvProvider.DTOs.Results;

public class ExternalChannel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Country { get; set; }
    public List<string> Categories { get; set; } = [];
    public string Logo { get; set; }
}
