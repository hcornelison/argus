using System.Text.Json;
using StackExchange.Redis;

namespace Argus.Codex.Redis;

/// <summary>
/// Wraps Redis Streams for all time-series data: metrics, processes, events, and log lines.
/// Each host gets four streams keyed by host ID. Log file paths are tracked in a per-host Set
/// so the file-picker query is O(1).
///
/// Retention is enforced two ways:
///   1. MAXLEN ~ 500_000 on every XADD (hard cap, prevents unbounded growth).
///   2. XTRIM MINID called by RetentionBackgroundService for time-accurate cleanup.
/// </summary>
public class RedisStreamService
{
    private const int MaxStreamLen = 500_000;

    private readonly IDatabase _db;

    public RedisStreamService(IConnectionMultiplexer mux) => _db = mux.GetDatabase();

    // --- Key helpers ---
    public static string MetricsKey(long hostId) => $"metrics:{hostId}";
    public static string ProcessesKey(long hostId) => $"processes:{hostId}";
    public static string EventsKey(long hostId) => $"events:{hostId}";
    public static string LogsKey(long hostId) => $"logs:{hostId}";
    public static string LogFilesKey(long hostId) => $"logfiles:{hostId}";

    // --- Append ---

    public Task AppendMetricAsync(long hostId, MetricPoint p) =>
        _db.StreamAddAsync(
            MetricsKey(hostId),
            [
                new("ts",        p.TimestampUtc.ToString("O")),
                new("cpu",       p.CpuPercent),
                new("mem_total", p.MemoryTotalBytes),
                new("mem_used",  p.MemoryUsedBytes),
                new("disks",     JsonSerializer.Serialize(p.Disks)),
            ],
            maxLength: MaxStreamLen, useApproximateMaxLength: true);

    public Task AppendProcessSnapshotAsync(long hostId, DateTime ts, IEnumerable<ProcessPoint> processes) =>
        _db.StreamAddAsync(
            ProcessesKey(hostId),
            [
                new("ts",    ts.ToString("O")),
                new("procs", JsonSerializer.Serialize(processes)),
            ],
            maxLength: MaxStreamLen, useApproximateMaxLength: true);

    public Task AppendEventAsync(long hostId, EventPoint p) =>
        _db.StreamAddAsync(
            EventsKey(hostId),
            [
                new("ts",       p.TimestampUtc.ToString("O")),
                new("channel",  p.Channel),
                new("source",   p.Source),
                new("level",    p.Level),
                new("event_id", p.EventId),
                new("msg",      p.Message),
            ],
            maxLength: MaxStreamLen, useApproximateMaxLength: true);

    public async Task AppendLogAsync(long hostId, LogPoint p)
    {
        await _db.StreamAddAsync(
            LogsKey(hostId),
            [
                new("ts",    p.TimestampUtc.ToString("O")),
                new("file",  p.FilePath),
                new("line",  p.Line),
                new("level", p.Level),
            ],
            maxLength: MaxStreamLen, useApproximateMaxLength: true);

        await _db.SetAddAsync(LogFilesKey(hostId), p.FilePath);
    }

    // --- Query ---

    public async Task<IReadOnlyList<MetricPoint>> GetMetricsAsync(long hostId, DateTime from, DateTime to)
    {
        var entries = await _db.StreamRangeAsync(MetricsKey(hostId), ToStreamId(from), ToStreamId(to, end: true));
        return entries.Select(e => new MetricPoint(
            ParseTs(e["ts"]),
            (double)e["cpu"],
            (long)e["mem_total"],
            (long)e["mem_used"],
            JsonSerializer.Deserialize<DiskPoint[]>((string)e["disks"]!)!
        )).ToList();
    }

    public async Task<ProcessSnapshot?> GetLatestProcessSnapshotAsync(long hostId, DateTime? at = null)
    {
        var maxId = at.HasValue ? ToStreamId(at.Value, end: true) : "+";
        var entries = await _db.StreamRangeAsync(ProcessesKey(hostId), "-", maxId, count: 1, messageOrder: Order.Descending);
        if (entries.Length == 0) return null;
        var e = entries[0];
        return new ProcessSnapshot(
            ParseTs(e["ts"]),
            JsonSerializer.Deserialize<ProcessPoint[]>((string)e["procs"]!)!);
    }

    public async Task<IReadOnlyList<EventPoint>> GetEventsAsync(long hostId, DateTime from, DateTime to, string? level = null)
    {
        var entries = await _db.StreamRangeAsync(EventsKey(hostId), ToStreamId(from), ToStreamId(to, end: true), count: 1000, messageOrder: Order.Descending);
        IEnumerable<StreamEntry> seq = entries;
        if (!string.IsNullOrWhiteSpace(level))
            seq = seq.Where(e => (string?)e["level"] == level);
        return seq.Select(e => new EventPoint(
            ParseTs(e["ts"]),
            e["channel"]!, e["source"]!, e["level"]!,
            (int)e["event_id"],
            e["msg"]!)).ToList();
    }

    public async Task<IReadOnlyList<LogPoint>> GetLogsAsync(
        long hostId, DateTime from, DateTime to,
        string? filePath = null, string? search = null)
    {
        var entries = await _db.StreamRangeAsync(LogsKey(hostId), ToStreamId(from), ToStreamId(to, end: true), count: 2000, messageOrder: Order.Descending);
        IEnumerable<StreamEntry> seq = entries;
        if (!string.IsNullOrWhiteSpace(filePath))
            seq = seq.Where(e => (string?)e["file"] == filePath);
        if (!string.IsNullOrWhiteSpace(search))
            seq = seq.Where(e => ((string?)e["line"])?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        return seq.Select(e => new LogPoint(
            ParseTs(e["ts"]), e["file"]!, e["line"]!, e["level"]!)).ToList();
    }

    public async Task<IReadOnlyList<ProcessHistoryPoint>> GetProcessHistoryAsync(
        long hostId, string name, DateTime from, DateTime to)
    {
        var entries = await _db.StreamRangeAsync(ProcessesKey(hostId), ToStreamId(from), ToStreamId(to, end: true));
        var results = new List<ProcessHistoryPoint>();
        foreach (var e in entries)
        {
            var ts = ParseTs(e["ts"]);
            var procs = JsonSerializer.Deserialize<ProcessPoint[]>((string)e["procs"]!)!;
            var match = procs.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                results.Add(new ProcessHistoryPoint(ts, match.CpuPercent, match.MemoryBytes, match.ThreadCount));
        }
        return results;
    }

    public async Task<IReadOnlyList<string>> GetLogFilesAsync(long hostId)
    {
        var members = await _db.SetMembersAsync(LogFilesKey(hostId));
        return members.Select(m => (string)m!).OrderBy(s => s).ToList();
    }

    // --- Retention ---

    public async Task TrimStreamsForHostAsync(long hostId, DateTime cutoff)
    {
        var minId = $"{ToUnixMs(cutoff)}-0";
        string[] keys = [MetricsKey(hostId), ProcessesKey(hostId), EventsKey(hostId), LogsKey(hostId)];
        foreach (var key in keys)
            await _db.ExecuteAsync("XTRIM", key, "MINID", "~", minId);
    }

    // --- Helpers ---

    private static string ToStreamId(DateTime dt, bool end = false)
    {
        var ms = ToUnixMs(dt);
        return end ? $"{ms}-9999999" : $"{ms}-0";
    }

    private static long ToUnixMs(DateTime dt) =>
        new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static DateTime ParseTs(RedisValue v) =>
        DateTime.Parse((string)v!, null, System.Globalization.DateTimeStyles.RoundtripKind);
}
