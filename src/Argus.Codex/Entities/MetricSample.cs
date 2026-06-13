namespace Argus.Codex.Entities;

/// <summary>Point-in-time overall resource usage for a host.</summary>
public class MetricSample
{
    public long Id { get; set; }
    public long HostId { get; set; }
    public Host? Host { get; set; }

    public DateTime TimestampUtc { get; set; }
    public double CpuPercent { get; set; }
    public long MemoryTotalBytes { get; set; }
    public long MemoryUsedBytes { get; set; }
}
