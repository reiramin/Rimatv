using Utilities.Models.Updates;

namespace iptv.Services._Channel.DTOs.Updates;

public class GetAllChannelUpdate : PaginationUpdate
{
    public string Search { get; set; }
    public bool OrderBy { get; set; }
}