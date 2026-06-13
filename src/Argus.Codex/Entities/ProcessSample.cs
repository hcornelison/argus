namespace Argus.Codex.Entities;

/// <summary>Per-process resource usage captured in a host snapshot.</summary>
public class ProcessSample
{
    public long Id { get; set; }
    public long HostId { get; set; }
    public Host? Host { get; set; }

    public DateTime TimestampUtc { get; set; }
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public long MemoryBytes { get; set; }
    public int ThreadCount { get; set; }
}
