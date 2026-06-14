using Argus.Codex;
using Argus.Codex.Redis;
using Microsoft.EntityFrameworkCore;

namespace Argus.Styx.Endpoints;

/// <summary>
/// REST query API consumed by pantheon. All routes hang off /api and run under the
/// "ui" authorization policy, which is a permissive no-op today but is the single
/// place to require OIDC bearer auth later.
///
/// Host records come from SQLite; all time-series data comes from Redis Streams.
/// </summary>
public static class QueryEndpoints
{
    public static void MapArgusApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization("ui");

        api.MapGet("/hosts", async (ArgusDbContext db, CancellationToken ct) =>
        {
            var hosts = await db.Hosts.OrderBy(h => h.MachineName).ToListAsync(ct);
            return hosts.Select(h => new
            {
                h.Id, h.MachineName, h.OperatingSystem, h.AgentVersion,
                firstSeenUtc = DateTime.SpecifyKind(h.FirstSeenUtc, DateTimeKind.Utc),
                lastSeenUtc  = DateTime.SpecifyKind(h.LastSeenUtc,  DateTimeKind.Utc),
            });
        });

        api.MapGet("/hosts/{id:long}", async (long id, ArgusDbContext db, CancellationToken ct) =>
        {
            var h = await db.Hosts.FindAsync([id], ct);
            if (h is null) return Results.NotFound();
            return Results.Ok(new
            {
                h.Id, h.MachineName, h.OperatingSystem, h.AgentVersion,
                firstSeenUtc = DateTime.SpecifyKind(h.FirstSeenUtc, DateTimeKind.Utc),
                lastSeenUtc  = DateTime.SpecifyKind(h.LastSeenUtc,  DateTimeKind.Utc),
            });
        });

        api.MapGet("/hosts/{id:long}/metrics", async (long id, DateTime? from, DateTime? to, RedisStreamService redis, CancellationToken ct) =>
        {
            var (f, t) = Window(from, to);
            var metrics = await redis.GetMetricsAsync(id, f, t);
            return Results.Ok(new
            {
                metrics = metrics.Select(m => new
                {
                    timestampUtc = DateTime.SpecifyKind(m.TimestampUtc, DateTimeKind.Utc),
                    m.CpuPercent, m.MemoryTotalBytes, m.MemoryUsedBytes,
                }),
                disks = metrics.SelectMany(m => m.Disks.Select(d => new
                {
                    timestampUtc = DateTime.SpecifyKind(m.TimestampUtc, DateTimeKind.Utc),
                    d.Mount, d.TotalBytes, d.UsedBytes,
                })),
            });
        });

        api.MapGet("/hosts/{id:long}/processes", async (long id, DateTime? at, RedisStreamService redis, CancellationToken ct) =>
        {
            var snap = await redis.GetLatestProcessSnapshotAsync(id, at);
            if (snap is null)
                return Results.Ok(new { timestampUtc = (DateTime?)null, processes = Array.Empty<object>() });

            return Results.Ok(new
            {
                timestampUtc = snap.TimestampUtc,
                processes = snap.Processes
                    .OrderByDescending(p => p.CpuPercent)
                    .Select(p => new { p.Pid, p.Name, p.CpuPercent, p.MemoryBytes, p.ThreadCount }),
            });
        });

        api.MapGet("/hosts/{id:long}/process-history", async (long id, string name, DateTime? from, DateTime? to, RedisStreamService redis, CancellationToken ct) =>
        {
            var (f, t) = Window(from, to);
            var points = await redis.GetProcessHistoryAsync(id, name, f, t);
            return points.Select(p => new
            {
                timestampUtc = DateTime.SpecifyKind(p.TimestampUtc, DateTimeKind.Utc),
                p.CpuPercent, p.MemoryBytes, p.ThreadCount,
            });
        });

        api.MapGet("/hosts/{id:long}/eventlogs", async (long id, DateTime? from, DateTime? to, string? level, RedisStreamService redis, CancellationToken ct) =>
        {
            var (f, t) = Window(from, to);
            return await redis.GetEventsAsync(id, f, t, level);
        });

        api.MapGet("/logs", async (long? hostId, string? filePath, DateTime? from, DateTime? to, string? q, ArgusDbContext db, RedisStreamService redis, CancellationToken ct) =>
        {
            var (f, t) = Window(from, to);
            var hostIds = hostId.HasValue
                ? [hostId.Value]
                : await db.Hosts.Select(h => h.Id).ToListAsync(ct);

            var tasks = hostIds.Select(id => redis.GetLogsAsync(id, f, t, filePath, q));
            var results = await Task.WhenAll(tasks);
            return results.SelectMany(r => r)
                .OrderByDescending(l => l.TimestampUtc)
                .Take(2000)
                .Select(l => new { l.TimestampUtc, l.FilePath, l.Line, l.Level });
        });

        api.MapGet("/eventlogs", async (long? hostId, string? level, string? channel, DateTime? from, DateTime? to, string? q, ArgusDbContext db, RedisStreamService redis, CancellationToken ct) =>
        {
            var (f, t) = Window(from, to);
            var hostIds = hostId.HasValue
                ? [hostId.Value]
                : await db.Hosts.Select(h => h.Id).ToListAsync(ct);

            var tasks = hostIds.Select(id => redis.GetEventsAsync(id, f, t, level));
            var results = await Task.WhenAll(tasks);
            return results.SelectMany(r => r)
                .Where(e => string.IsNullOrWhiteSpace(channel) || e.Channel == channel)
                .Where(e => string.IsNullOrWhiteSpace(q) || e.Message.Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.TimestampUtc)
                .Take(2000)
                .Select(e => new { e.TimestampUtc, e.Channel, e.Source, e.Level, e.EventId, e.Message });
        });

        api.MapGet("/logfiles", async (long? hostId, ArgusDbContext db, RedisStreamService redis, CancellationToken ct) =>
        {
            var hostIds = hostId.HasValue
                ? [hostId.Value]
                : await db.Hosts.Select(h => h.Id).ToListAsync(ct);

            var tasks = hostIds.Select(id => redis.GetLogFilesAsync(id));
            var results = await Task.WhenAll(tasks);
            return results.SelectMany(r => r).Distinct().OrderBy(p => p);
        });
    }

    /// <summary>Defaults the query window to the last 5 minutes when no bounds are given.</summary>
    private static (DateTime From, DateTime To) Window(DateTime? from, DateTime? to)
    {
        var t = to ?? DateTime.UtcNow;
        var f = from ?? t.AddMinutes(-5);
        return (f, t);
    }
}
