using System.Diagnostics;

namespace Argus.Herald.Collectors;

public record ProcessUsage(int Pid, string Name, double CpuPercent, long MemoryBytes, int ThreadCount);

/// <summary>
/// Cross-platform per-process sampling via System.Diagnostics.Process. CPU% is derived
/// from the delta in TotalProcessorTime since the previous snapshot, normalized by elapsed
/// wall time and processor count.
/// </summary>
public class ProcessCollector
{
    private readonly int _cpuCount = Environment.ProcessorCount;
    private Dictionary<int, TimeSpan> _previous = new();
    private DateTime _previousAt = DateTime.UtcNow;

    public List<ProcessUsage> Collect(int maxProcesses)
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _previousAt).TotalSeconds;
        var current = new Dictionary<int, TimeSpan>();
        var result = new List<ProcessUsage>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var cpuTime = p.TotalProcessorTime;
                current[p.Id] = cpuTime;

                double cpuPercent = 0;
                if (elapsed > 0 && _previous.TryGetValue(p.Id, out var prev))
                {
                    var deltaSec = (cpuTime - prev).TotalSeconds;
                    cpuPercent = Math.Clamp(deltaSec / (elapsed * _cpuCount) * 100.0, 0, 100);
                }

                result.Add(new ProcessUsage(p.Id, p.ProcessName, Math.Round(cpuPercent, 2),
                    p.WorkingSet64, p.Threads.Count));
            }
            catch
            {
                // Process may have exited or be inaccessible; skip.
            }
            finally
            {
                p.Dispose();
            }
        }

        _previous = current;
        _previousAt = now;

        IEnumerable<ProcessUsage> ordered = result.OrderByDescending(p => p.MemoryBytes);
        if (maxProcesses > 0) ordered = ordered.Take(maxProcesses);
        return ordered.ToList();
    }
}
