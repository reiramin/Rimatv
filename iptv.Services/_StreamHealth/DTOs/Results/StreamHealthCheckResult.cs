namespace iptv.Services._StreamHealth.DTOs.Results;

public class StreamHealthCheckResult
{
    public int CheckedCount { get; set; }
    public int HealthyCount { get; set; }
    public int UnhealthyCount { get; set; }
}
