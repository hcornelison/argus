using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Argus.Herald.Collectors;

/// <summary>
/// macOS CPU and memory via Mach host_statistics and sysctl. Mirrors the Linux collector's
/// approach: CPU is sampled over a short interval; memory used = total - available, where
/// available counts free + inactive (reclaimable) pages, akin to Linux MemAvailable.
/// </summary>
[SupportedOSPlatform("osx")]
public class MacResourceCollector : IResourceCollector
{
    // Mach host_statistics flavors.
    private const int HOST_CPU_LOAD_INFO = 3;
    private const int HOST_CPU_LOAD_INFO_COUNT = 4;   // [user, system, idle, nice]
    private const int CPU_STATE_IDLE = 2;

    private const int HOST_VM_INFO = 2;
    private const int HOST_VM_INFO_COUNT = 15;        // vm_statistics (32-bit) field count
    // Indices into vm_statistics: 0 free, 1 active, 2 inactive, 3 wire.
    private const int VM_FREE = 0;
    private const int VM_INACTIVE = 2;

    public async Task<ResourceSnapshot> CollectAsync(CancellationToken ct)
    {
        var (idle1, total1) = ReadCpuTicks();
        await Task.Delay(250, ct);
        var (idle2, total2) = ReadCpuTicks();

        var totalDelta = total2 - total1;
        var idleDelta = idle2 - idle1;
        var cpu = totalDelta > 0 ? (1.0 - (double)idleDelta / totalDelta) * 100.0 : 0.0;

        var (memTotal, memUsed) = ReadMemory();
        return new ResourceSnapshot(Math.Round(cpu, 2), memTotal, memUsed, DiskCollector.Collect());
    }

    private static (long Idle, long Total) ReadCpuTicks()
    {
        var info = new int[HOST_CPU_LOAD_INFO_COUNT];
        uint count = HOST_CPU_LOAD_INFO_COUNT;
        if (host_statistics(mach_host_self(), HOST_CPU_LOAD_INFO, info, ref count) != 0)
            return (0, 0);

        long total = 0;
        for (var i = 0; i < HOST_CPU_LOAD_INFO_COUNT; i++) total += (uint)info[i];
        return ((uint)info[CPU_STATE_IDLE], total);
    }

    private static (long Total, long Used) ReadMemory()
    {
        long total = 0;
        nuint len = sizeof(long);
        sysctlbyname("hw.memsize", ref total, ref len, IntPtr.Zero, 0);

        var pageSize = getpagesize();
        var info = new int[HOST_VM_INFO_COUNT];
        uint count = HOST_VM_INFO_COUNT;
        if (host_statistics(mach_host_self(), HOST_VM_INFO, info, ref count) != 0 || total == 0)
            return (total, 0);

        long free = (uint)info[VM_FREE];
        long inactive = (uint)info[VM_INACTIVE];
        var available = (free + inactive) * pageSize;
        var used = Math.Max(0, total - available);
        return (total, used);
    }

    [DllImport("libSystem.dylib")]
    private static extern uint mach_host_self();

    [DllImport("libSystem.dylib")]
    private static extern int host_statistics(uint hostPriv, int flavor, int[] info, ref uint count);

    [DllImport("libSystem.dylib")]
    private static extern int getpagesize();

    [DllImport("libSystem.dylib", CharSet = CharSet.Ansi)]
    private static extern int sysctlbyname(string name, ref long oldp, ref nuint oldlenp, IntPtr newp, nuint newlen);
}
