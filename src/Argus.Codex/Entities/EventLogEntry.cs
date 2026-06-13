namespace Argus.Codex.Entities;

/// <summary>An OS event log record (Windows Event Viewer / Linux journald).</summary>
public class EventLogEntry
{
    public long Id { get; set; }
    public long HostId { get; set; }
    public Host? Host { get; set; }

    public DateTime TimestampUtc { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string Message { get; set; } = string.Empty;
}
