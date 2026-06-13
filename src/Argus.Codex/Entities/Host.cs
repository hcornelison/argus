namespace Argus.Codex.Entities;

/// <summary>A monitored host running a herald agent.</summary>
public class Host
{
    public long Id { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the per-agent API key used for ingest auth.</summary>
    public string ApiKeyHash { get; set; } = string.Empty;

    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }

    public ICollection<MetricSample> MetricSamples { get; set; } = new List<MetricSample>();
    public ICollection<DiskSample> DiskSamples { get; set; } = new List<DiskSample>();
    public ICollection<ProcessSample> ProcessSamples { get; set; } = new List<ProcessSample>();
    public ICollection<EventLogEntry> EventLogEntries { get; set; } = new List<EventLogEntry>();
    public ICollection<LogEntry> LogEntries { get; set; } = new List<LogEntry>();
}
