namespace Argus.Codex.Entities;

/// <summary>Usage for a single disk/volume at a point in time.</summary>
public class DiskSample
{
    public long Id { get; set; }
    public long HostId { get; set; }
    public Host? Host { get; set; }

    public DateTime TimestampUtc { get; set; }
    public string Mount { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
}
