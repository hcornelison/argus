using Argus.Codex;
using Argus.Codex.Redis;
using Argus.Contracts;
using Argus.Styx.Hubs;
using Grpc.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Argus.Styx.Grpc;

/// <summary>
/// Server side of the herald -> styx ingest contract. Writes to Redis Streams and pushes
/// live updates to pantheon. Host registration touches SQLite. Any machine running a
/// herald agent is accepted.
/// </summary>
public class IngestServiceImpl : IngestService.IngestServiceBase
{
    private readonly ArgusDbContext _db;
    private readonly RedisStreamService _redis;
    private readonly IHubContext<LiveHub> _hub;
    private readonly ILogger<IngestServiceImpl> _logger;

    public IngestServiceImpl(ArgusDbContext db, RedisStreamService redis, IHubContext<LiveHub> hub, ILogger<IngestServiceImpl> logger)
    {
        _db = db;
        _redis = redis;
        _hub = hub;
        _logger = logger;
    }

    public override async Task<RegisterHostResponse> RegisterHost(RegisterHostRequest request, ServerCallContext context)
    {
        var now = DateTime.UtcNow;

        var host = await _db.Hosts.FirstOrDefaultAsync(h => h.MachineName == request.MachineName, context.CancellationToken);
        if (host is null)
        {
            host = new Argus.Codex.Entities.Host { FirstSeenUtc = now };
            _db.Hosts.Add(host);
        }

        host.MachineName = request.MachineName;
        host.OperatingSystem = request.OperatingSystem;
        host.AgentVersion = request.AgentVersion;
        host.LastSeenUtc = now;

        await _db.SaveChangesAsync(context.CancellationToken);
        _logger.LogInformation("Registered host {Host} (id {Id})", host.MachineName, host.Id);

        return new RegisterHostResponse { HostId = host.Id };
    }

    public override async Task<IngestAck> StreamMetrics(IAsyncStreamReader<MetricBatch> requestStream, ServerCallContext context)
    {
        long accepted = 0;
        await foreach (var batch in requestStream.ReadAllAsync(context.CancellationToken))
        {
            foreach (var s in batch.Samples)
            {
                var point = new MetricPoint(
                    s.Timestamp.ToDateTime(),
                    s.CpuPercent,
                    s.MemoryTotalBytes,
                    s.MemoryUsedBytes,
                    s.Disks.Select(d => new DiskPoint(d.Mount, d.TotalBytes, d.UsedBytes)).ToArray());

                await _redis.AppendMetricAsync(batch.HostId, point);
                accepted++;
            }

            await TouchHostAsync(batch.HostId, context.CancellationToken);

            var last = batch.Samples.LastOrDefault();
            if (last is not null)
            {
                await _hub.Clients.Group(LiveHub.GroupFor(batch.HostId))
                    .SendAsync("metric", new
                    {
                        hostId = batch.HostId,
                        timestampUtc = last.Timestamp.ToDateTime(),
                        cpuPercent = last.CpuPercent,
                        memoryTotalBytes = last.MemoryTotalBytes,
                        memoryUsedBytes = last.MemoryUsedBytes,
                        disks = last.Disks.Select(d => new { d.Mount, d.TotalBytes, d.UsedBytes }),
                    }, context.CancellationToken);
            }
        }
        return new IngestAck { Accepted = accepted };
    }

    public override async Task<IngestAck> StreamProcesses(IAsyncStreamReader<ProcessBatch> requestStream, ServerCallContext context)
    {
        long accepted = 0;
        await foreach (var batch in requestStream.ReadAllAsync(context.CancellationToken))
        {
            var ts = batch.Timestamp.ToDateTime();
            var processes = batch.Processes
                .Select(p => new ProcessPoint(p.Pid, p.Name, p.CpuPercent, p.MemoryBytes, p.ThreadCount))
                .ToArray();

            await _redis.AppendProcessSnapshotAsync(batch.HostId, ts, processes);
            accepted += batch.Processes.Count;

            await TouchHostAsync(batch.HostId, context.CancellationToken);

            await _hub.Clients.Group(LiveHub.GroupFor(batch.HostId))
                .SendAsync("processes", new
                {
                    hostId = batch.HostId,
                    timestampUtc = ts,
                    processes = batch.Processes.Select(p => new { p.Pid, p.Name, p.CpuPercent, p.MemoryBytes, p.ThreadCount }),
                }, context.CancellationToken);
        }
        return new IngestAck { Accepted = accepted };
    }

    public override async Task<IngestAck> StreamEvents(IAsyncStreamReader<EventBatch> requestStream, ServerCallContext context)
    {
        long accepted = 0;
        await foreach (var batch in requestStream.ReadAllAsync(context.CancellationToken))
        {
            foreach (var e in batch.Entries)
            {
                await _redis.AppendEventAsync(batch.HostId, new EventPoint(
                    e.Timestamp.ToDateTime(), e.Channel, e.Source, e.Level, e.EventId, e.Message));
                accepted++;
            }

            await TouchHostAsync(batch.HostId, context.CancellationToken);

            if (batch.Entries.Count > 0)
            {
                await _hub.Clients.Group(LiveHub.GroupFor(batch.HostId))
                    .SendAsync("events", new
                    {
                        hostId = batch.HostId,
                        entries = batch.Entries.Select(e => new
                        {
                            timestampUtc = e.Timestamp.ToDateTime(),
                            e.Channel, e.Source, e.Level, e.EventId, e.Message,
                        }),
                    }, context.CancellationToken);
            }
        }
        return new IngestAck { Accepted = accepted };
    }

    public override async Task<IngestAck> StreamLogs(IAsyncStreamReader<LogBatch> requestStream, ServerCallContext context)
    {
        long accepted = 0;
        await foreach (var batch in requestStream.ReadAllAsync(context.CancellationToken))
        {
            foreach (var l in batch.Lines)
            {
                await _redis.AppendLogAsync(batch.HostId, new LogPoint(
                    l.Timestamp.ToDateTime(), l.FilePath, l.Line, l.Level));
                accepted++;
            }

            await TouchHostAsync(batch.HostId, context.CancellationToken);

            if (batch.Lines.Count > 0)
            {
                await _hub.Clients.Group(LiveHub.GroupFor(batch.HostId))
                    .SendAsync("logs", new
                    {
                        hostId = batch.HostId,
                        lines = batch.Lines.Select(l => new
                        {
                            timestampUtc = l.Timestamp.ToDateTime(), l.FilePath, l.Line, l.Level,
                        }),
                    }, context.CancellationToken);
            }
        }
        return new IngestAck { Accepted = accepted };
    }

    private Task TouchHostAsync(long hostId, CancellationToken ct) =>
        _db.Hosts.Where(h => h.Id == hostId)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.LastSeenUtc, DateTime.UtcNow), ct);
}
