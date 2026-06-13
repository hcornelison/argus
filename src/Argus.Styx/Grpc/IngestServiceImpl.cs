using Argus.Codex;
using Entity = Argus.Codex.Entities;
using Argus.Contracts;
using Argus.Styx.Hubs;
using Argus.Styx.Security;
using Grpc.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Argus.Styx.Grpc;

/// <summary>
/// Server side of the herald -> styx ingest contract. The ApiKeyInterceptor has already
/// validated the caller; here we persist samples and push live updates to pantheon.
/// </summary>
public class IngestServiceImpl : IngestService.IngestServiceBase
{
    private readonly ArgusDbContext _db;
    private readonly IHubContext<LiveHub> _hub;
    private readonly ILogger<IngestServiceImpl> _logger;

    public IngestServiceImpl(ArgusDbContext db, IHubContext<LiveHub> hub, ILogger<IngestServiceImpl> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    private static string ApiKeyHash(ServerCallContext context)
        => (string)context.UserState[ApiKeyInterceptor.UserStateKey];

    public override async Task<RegisterHostResponse> RegisterHost(RegisterHostRequest request, ServerCallContext context)
    {
        var hash = ApiKeyHash(context);
        var now = DateTime.UtcNow;

        var host = await _db.Hosts.FirstOrDefaultAsync(h => h.ApiKeyHash == hash, context.CancellationToken);
        if (host is null)
        {
            host = new Entity.Host { ApiKeyHash = hash, FirstSeenUtc = now };
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
                var ts = s.Timestamp.ToDateTime();
                _db.MetricSamples.Add(new Entity.MetricSample
                {
                    HostId = batch.HostId,
                    TimestampUtc = ts,
                    CpuPercent = s.CpuPercent,
                    MemoryTotalBytes = s.MemoryTotalBytes,
                    MemoryUsedBytes = s.MemoryUsedBytes,
                });
                foreach (var d in s.Disks)
                {
                    _db.DiskSamples.Add(new Entity.DiskSample
                    {
                        HostId = batch.HostId,
                        TimestampUtc = ts,
                        Mount = d.Mount,
                        TotalBytes = d.TotalBytes,
                        UsedBytes = d.UsedBytes,
                    });
                }
                accepted++;
            }

            await _db.SaveChangesAsync(context.CancellationToken);
            await TouchHostAsync(batch.HostId, context.CancellationToken);
            _db.ChangeTracker.Clear();

            // Push the latest sample to subscribed dashboards.
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
            foreach (var p in batch.Processes)
            {
                _db.ProcessSamples.Add(new Entity.ProcessSample
                {
                    HostId = batch.HostId,
                    TimestampUtc = ts,
                    Pid = p.Pid,
                    Name = p.Name,
                    CpuPercent = p.CpuPercent,
                    MemoryBytes = p.MemoryBytes,
                    ThreadCount = p.ThreadCount,
                });
                accepted++;
            }
            await _db.SaveChangesAsync(context.CancellationToken);
            await TouchHostAsync(batch.HostId, context.CancellationToken);
            _db.ChangeTracker.Clear();

            await _hub.Clients.Group(LiveHub.GroupFor(batch.HostId))
                .SendAsync("processes", new
                {
                    hostId = batch.HostId,
                    timestampUtc = ts,
                    processes = batch.Processes.Select(p => new
                    {
                        p.Pid, p.Name, p.CpuPercent, p.MemoryBytes, p.ThreadCount,
                    }),
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
                _db.EventLogEntries.Add(new Entity.EventLogEntry
                {
                    HostId = batch.HostId,
                    TimestampUtc = e.Timestamp.ToDateTime(),
                    Channel = e.Channel,
                    Source = e.Source,
                    Level = e.Level,
                    EventId = e.EventId,
                    Message = e.Message,
                });
                accepted++;
            }
            await _db.SaveChangesAsync(context.CancellationToken);
            await TouchHostAsync(batch.HostId, context.CancellationToken);
            _db.ChangeTracker.Clear();

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
            var added = new List<Entity.LogEntry>();
            foreach (var l in batch.Lines)
            {
                var entry = new Entity.LogEntry
                {
                    HostId = batch.HostId,
                    TimestampUtc = l.Timestamp.ToDateTime(),
                    FilePath = l.FilePath,
                    Line = l.Line,
                    Level = l.Level,
                };
                _db.LogEntries.Add(entry);
                added.Add(entry);
                accepted++;
            }
            await _db.SaveChangesAsync(context.CancellationToken);
            await TouchHostAsync(batch.HostId, context.CancellationToken);
            _db.ChangeTracker.Clear();

            if (added.Count > 0)
            {
                await _hub.Clients.Group(LiveHub.GroupFor(batch.HostId))
                    .SendAsync("logs", new
                    {
                        hostId = batch.HostId,
                        lines = added.Select(l => new
                        {
                            l.TimestampUtc, l.FilePath, l.Line, l.Level,
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
