namespace Argus.Herald.Collectors;

public record DiskUsage(string Mount, long TotalBytes, long UsedBytes);

public record ResourceSnapshot(
    double CpuPercent,
    long MemoryTotalBytes,
    long MemoryUsedBytes,
    IReadOnlyList<DiskUsage> Disks);

public interface IResourceCollector
{
    /// <summary>Samples overall CPU/RAM/disk. CPU is measured over a short interval.</summary>
    Task<ResourceSnapshot> CollectAsync(CancellationToken ct);
}
