namespace Argus.Codex.Entities;

/// <summary>A single line shipped from a tailed log file.</summary>
public class LogEntry
{
    public long Id { get; set; }
    public long HostId { get; set; }
    public Host? Host { get; set; }

    public DateTime TimestampUtc { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Line { get; set; } = string.Empty;

    /// <summary>Best-effort parsed level; may be empty.</summary>
    public string Level { get; set; } = string.Empty;
}
