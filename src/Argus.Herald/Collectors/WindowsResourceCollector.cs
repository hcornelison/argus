using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Argus.Herald.Collectors;

/// <summary>Uses Win32 GetSystemTimes and GlobalMemoryStatusEx for CPU and memory.</summary>
[SupportedOSPlatform("windows")]
public class WindowsResourceCollector : IResourceCollector
{
    public async Task<ResourceSnapshot> CollectAsync(CancellationToken ct)
    {
        var t1 = ReadSystemTimes();
        await Task.Delay(250, ct);
        var t2 = ReadSystemTimes();

        var idle = t2.Idle - t1.Idle;
        var kernel = t2.Kernel - t1.Kernel;
        var user = t2.User - t1.User;
        var total = kernel + user; // kernel time already includes idle
        var cpu = total > 0 ? (1.0 - (double)idle / total) * 100.0 : 0.0;

        var (memTotal, memAvail) = ReadMemory();
        return new ResourceSnapshot(Math.Round(cpu, 2), memTotal, memTotal - memAvail, DiskCollector.Collect());
    }

    private static (ulong Idle, ulong Kernel, ulong User) ReadSystemTimes()
    {
        GetSystemTimes(out var idle, out var kernel, out var user);
        return (ToUlong(idle), ToUlong(kernel), ToUlong(user));
    }

    private static ulong ToUlong(FILETIME ft) => ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;

    private static (long Total, long Available) ReadMemory()
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref mem);
        return ((long)mem.ullTotalPhys, (long)mem.ullAvailPhys);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME { public int dwLowDateTime; public int dwHighDateTime; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
