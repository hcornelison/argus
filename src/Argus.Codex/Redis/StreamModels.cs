namespace Argus.Codex.Redis;

public record MetricPoint(
    DateTime TimestampUtc,
    double CpuPercent,
    long MemoryTotalBytes,
    long MemoryUsedBytes,
    DiskPoint[] Disks);

public record DiskPoint(string Mount, long TotalBytes, long UsedBytes);

public record ProcessSnapshot(DateTime TimestampUtc, ProcessPoint[] Processes);

public record ProcessPoint(int Pid, string Name, double CpuPercent, long MemoryBytes, int ThreadCount);

public record EventPoint(
    DateTime TimestampUtc,
    string Channel,
    string Source,
    string Level,
    int EventId,
    string Message);

public record LogPoint(DateTime TimestampUtc, string FilePath, string Line, string Level);
