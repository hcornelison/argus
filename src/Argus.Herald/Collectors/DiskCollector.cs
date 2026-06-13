using System.Runtime.InteropServices;

namespace Argus.Herald.Collectors;

/// <summary>Cross-platform disk usage via DriveInfo; fixed/ready drives only.</summary>
public static class DiskCollector
{
    public static List<DiskUsage> Collect()
    {
        var disks = new List<DiskUsage>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady) continue;
                if (d.DriveType != DriveType.Fixed) continue;
                if (d.TotalSize <= 0) continue;
                if (IsNoise(d.Name)) continue;
                var used = d.TotalSize - d.TotalFreeSpace;
                disks.Add(new DiskUsage(d.Name, d.TotalSize, used));
            }
            catch
            {
                // Skip drives we can't read (permissions, transient).
            }
        }
        return disks;
    }

    /// <summary>
    /// On macOS, APFS exposes many synthetic firmlink volumes under /System/Volumes that all
    /// report the same container, so we keep only "/" and real mounts under /Volumes.
    /// </summary>
    private static bool IsNoise(string mount)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return false;
        return mount.StartsWith("/System/Volumes/", StringComparison.Ordinal)
            || mount.StartsWith("/private/", StringComparison.Ordinal);
    }
}
