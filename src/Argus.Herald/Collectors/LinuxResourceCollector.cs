namespace Argus.Herald.Collectors;

/// <summary>Reads /proc/stat and /proc/meminfo for CPU and memory.</summary>
public class LinuxResourceCollector : IResourceCollector
{
    public async Task<ResourceSnapshot> CollectAsync(CancellationToken ct)
    {
        var (idle1, total1) = ReadCpuTimes();
        await Task.Delay(250, ct);
        var (idle2, total2) = ReadCpuTimes();

        var totalDelta = total2 - total1;
        var idleDelta = idle2 - idle1;
        var cpu = totalDelta > 0 ? (1.0 - (double)idleDelta / totalDelta) * 100.0 : 0.0;

        var (memTotal, memAvailable) = ReadMemInfo();
        var memUsed = memTotal - memAvailable;

        return new ResourceSnapshot(Math.Round(cpu, 2), memTotal, memUsed, DiskCollector.Collect());
    }

    private static (long Idle, long Total) ReadCpuTimes()
    {
        // First line of /proc/stat: "cpu user nice system idle iowait irq softirq steal ..."
        var line = File.ReadLines("/proc/stat").First();
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        long total = 0, idle = 0;
        for (var i = 1; i < parts.Length; i++)
        {
            if (!long.TryParse(parts[i], out var v)) continue;
            total += v;
            if (i == 4 || i == 5) idle += v; // idle + iowait
        }
        return (idle, total);
    }

    private static (long Total, long Available) ReadMemInfo()
    {
        long total = 0, available = 0;
        foreach (var line in File.ReadLines("/proc/meminfo"))
        {
            if (line.StartsWith("MemTotal:")) total = ParseKb(line);
            else if (line.StartsWith("MemAvailable:")) available = ParseKb(line);
            if (total > 0 && available > 0) break;
        }
        return (total, available);

        static long ParseKb(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && long.TryParse(parts[1], out var kb) ? kb * 1024 : 0;
        }
    }
}
