namespace Argus.Herald.Configuration;

public class HeraldOptions
{
    public const string SectionName = "Herald";

    /// <summary>styx gRPC endpoint, e.g. http://localhost:8081 (h2c) or https://styx:8081.</summary>
    public string StyxGrpcEndpoint { get; set; } = "http://localhost:8081";

    /// <summary>Per-agent API key sent in the x-argus-api-key header.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public int MetricsIntervalSeconds { get; set; } = 10;
    public int ProcessIntervalSeconds { get; set; } = 10;

    /// <summary>Cap on processes reported per snapshot (top-N by memory). 0 = all.</summary>
    public int MaxProcesses { get; set; } = 100;

    /// <summary>Files and/or directories to tail. Directories are watched recursively.</summary>
    public List<string> LogPaths { get; set; } = new();

    /// <summary>Glob-style file filter applied to directory paths.</summary>
    public string LogFileFilter { get; set; } = "*.log";

    /// <summary>Local file where per-file tail offsets are persisted across restarts.</summary>
    public string CheckpointFile { get; set; } = "herald-checkpoints.json";

    /// <summary>Max batches to buffer in memory when styx is unreachable.</summary>
    public int MaxBufferedBatches { get; set; } = 1000;

    /// <summary>OS event-log (Windows Event Viewer / Linux journald / macOS unified log) collection.</summary>
    public EventLogOptions EventLog { get; set; } = new();
}

public class EventLogOptions
{
    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Windows event-log channels to read (e.g. Application, System). "Security" needs admin.
    /// Ignored on Linux/macOS, which read the system journal / unified log.
    /// </summary>
    public List<string> Channels { get; set; } = new() { "Application", "System" };

    /// <summary>
    /// On Linux/macOS, only collect entries at or above this severity to limit volume.
    /// One of: Verbose, Information, Warning, Error, Critical.
    /// Note: macOS treats Warning/Error/Critical as fault-only, because the macOS "error"
    /// message type is extremely high-volume and mostly benign; set Information to include it.
    /// </summary>
    public string MinLevel { get; set; } = "Warning";

    /// <summary>
    /// Safety cap on records shipped per collection cycle (most recent kept). Guards against
    /// floods from very chatty hosts. 0 = unlimited.
    /// </summary>
    public int MaxRecordsPerCycle { get; set; } = 1000;
}
