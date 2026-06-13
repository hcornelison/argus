using Argus.Codex;
using Microsoft.EntityFrameworkCore;

namespace Argus.Styx.Endpoints;

/// <summary>
/// REST query API consumed by pantheon. All routes hang off /api and run under the
/// "ui" authorization policy, which is a permissive no-op today but is the single
/// place to require OIDC bearer auth later.
/// </summary>
public static class QueryEndpoints
{
    public static void MapArgusApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization("ui");

        api.MapGet("/hosts", async (ArgusDbContext db, CancellationToken ct) =>
            await db.Hosts
                .OrderBy(h => h.MachineName)
                .Select(h => new
                {
                    h.Id, h.MachineName, h.OperatingSystem, h.AgentVersion,
                    h.FirstSeenUtc, h.LastSeenUtc,
                })
                .ToListAsync(ct));

        api.MapGet("/hosts/{id:long}/metrics", async (long id, DateTime? from, DateTime? to, ArgusDbContext db, CancellationToken ct) =>
        {
            var (f, t) = Window(from, to);
            var metrics = await db.MetricSamples
                .Where(m => m.HostId == id && m.TimestampUtc >= f && m.TimestampUtc <= t)
                .OrderBy(m => m.TimestampUtc)
                .Select(m => new { m.TimestampUtc, m.CpuPercent, m.MemoryTotalBytes, m.MemoryUsedBytes })
                .ToListAsync(ct);
            var disks = await db.DiskSamples
                .Where(d => d.HostId == id && d.TimestampUtc >= f && d.TimestampUtc <= t)
                .OrderBy(d => d.TimestampUtc)
                .Select(d => new { d.TimestampUtc, d.Mount, d.TotalBytes, d.UsedBytes })
                .ToListAsync(ct);
            return Results.Ok(new { metrics, disks });
        });

        api.MapGet("/hosts/{id:long}/processes", async (long id, DateTime? at, ArgusDbContext db, CancellationToken ct) =>
        {
            // Snapshot timestamp at/just-before `at`, else the most recent snapshot.
            var q = db.ProcessSamples.Where(p => p.HostId == id);
            if (at is not null) q = q.Where(p => p.TimestampUtc <= at);
            var snapTs = await q.MaxAsync(p => (DateTime?)p.TimestampUtc, ct);
            if (snapTs is null) return Results.Ok(new { timestampUtc = (DateTime?)null, processes = Array.Empty<object>() });

            var processes = await db.ProcessSamples
                .Where(p => p.HostId == id && p.TimestampUtc == snapTs)
                .OrderByDescending(p => p.CpuPercent)
                .Select(p => new { p.Pid, p.Name, p.CpuPercent, p.MemoryBytes, p.ThreadCount })
                .ToListAsync(ct);
            return Results.Ok(new { timestampUtc = snapTs, processes });
        });

        api.MapGet("/hosts/{id:long}/eventlogs", async (long id, DateTime? from, DateTime? to, string? level, ArgusDbContext db, CancellationToken ct) =>
        {
            var (f, t) = Window(from, to);
            var q = db.EventLogEntries.Where(e => e.HostId == id && e.TimestampUtc >= f && e.TimestampUtc <= t);
            if (!string.IsNullOrWhiteSpace(level)) q = q.Where(e => e.Level == level);
            return await q.OrderByDescending(e => e.TimestampUtc).Take(1000)
                .Select(e => new { e.TimestampUtc, e.Channel, e.Source, e.Level, e.EventId, e.Message })
                .ToListAsync(ct);
        });

        api.MapGet("/logs", async (long? hostId, string? filePath, DateTime? from, DateTime? to, string? q, ArgusDbContext db, CancellationToken ct) =>
        {
            var (f, t) = Window(from, to);
            var query = db.LogEntries.Where(l => l.TimestampUtc >= f && l.TimestampUtc <= t);
            if (hostId is not null) query = query.Where(l => l.HostId == hostId);
            if (!string.IsNullOrWhiteSpace(filePath)) query = query.Where(l => l.FilePath == filePath);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(l => l.Line.Contains(q));
            return await query.OrderByDescending(l => l.TimestampUtc).Take(2000)
                .Select(l => new { l.HostId, l.TimestampUtc, l.FilePath, l.Line, l.Level })
                .ToListAsync(ct);
        });

        // Global event-log query (across hosts), mirroring /logs.
        api.MapGet("/eventlogs", async (long? hostId, string? level, string? channel, DateTime? from, DateTime? to, string? q, ArgusDbContext db, CancellationToken ct) =>
        {
            var (f, t) = Window(from, to);
            var query = db.EventLogEntries.Where(e => e.TimestampUtc >= f && e.TimestampUtc <= t);
            if (hostId is not null) query = query.Where(e => e.HostId == hostId);
            if (!string.IsNullOrWhiteSpace(level)) query = query.Where(e => e.Level == level);
            if (!string.IsNullOrWhiteSpace(channel)) query = query.Where(e => e.Channel == channel);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(e => e.Message.Contains(q));
            return await query.OrderByDescending(e => e.TimestampUtc).Take(2000)
                .Select(e => new { e.HostId, e.TimestampUtc, e.Channel, e.Source, e.Level, e.EventId, e.Message })
                .ToListAsync(ct);
        });

        // Distinct log file paths (optionally per host) to populate the log-viewer file picker.
        api.MapGet("/logfiles", async (long? hostId, ArgusDbContext db, CancellationToken ct) =>
        {
            var query = db.LogEntries.AsQueryable();
            if (hostId is not null) query = query.Where(l => l.HostId == hostId);
            return await query.Select(l => l.FilePath).Distinct().OrderBy(p => p).ToListAsync(ct);
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
