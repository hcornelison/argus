using Argus.Contracts;
using Argus.Herald.Collectors;
using Argus.Herald.Configuration;
using Argus.Herald.Ingest;
using Argus.Herald.LogShipping;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Argus.Herald.Workers;

/// <summary>
/// Periodically collects OS event-log records newer than the last checkpoint and ships them
/// to styx. The checkpoint (last query time, persisted as ticks) advances only after a
/// successful ship, giving at-least-once delivery across restarts.
/// </summary>
public class EventLogWorker : BackgroundService
{
    private const string CheckpointKey = "__eventlog_since__";

    private readonly IEventLogCollector _collector;
    private readonly IngestConnection _connection;
    private readonly EventLogOptions _options;
    private readonly CheckpointStore _checkpoints;
    private readonly ILogger<EventLogWorker> _logger;

    public EventLogWorker(IEventLogCollector collector, IngestConnection connection,
        IOptions<HeraldOptions> options, CheckpointStore checkpoints, ILogger<EventLogWorker> logger)
    {
        _collector = collector;
        _connection = connection;
        _options = options.Value.EventLog;
        _checkpoints = checkpoints;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Event-log collection disabled");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.IntervalSeconds)));
        do
        {
            try
            {
                if (!await _connection.EnsureRegisteredAsync(ct)) continue;

                var since = GetCheckpoint();
                var queryStart = DateTime.UtcNow;

                var records = await _collector.CollectSinceAsync(since, ct);
                records = Cap(records);
                if (records.Count > 0)
                {
                    if (!await ShipAsync(records, ct)) continue; // keep checkpoint; retry next tick
                }

                SetCheckpoint(queryStart);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event-log collection cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    /// <summary>Keeps only the most recent N records when a cycle exceeds the configured cap.</summary>
    private IReadOnlyList<EventLogRecord> Cap(IReadOnlyList<EventLogRecord> records)
    {
        var max = _options.MaxRecordsPerCycle;
        if (max <= 0 || records.Count <= max) return records;
        _logger.LogWarning("Event-log cycle produced {Total} records; capping to most recent {Max}", records.Count, max);
        return records.Skip(records.Count - max).ToList(); // records are oldest-first
    }

    private DateTime GetCheckpoint()
    {
        var ticks = _checkpoints.Get(CheckpointKey);
        // First run: small backfill so recent events show up promptly.
        return ticks < 0
            ? DateTime.UtcNow.AddMinutes(-2)
            : new DateTime(ticks, DateTimeKind.Utc);
    }

    private void SetCheckpoint(DateTime utc) => _checkpoints.Set(CheckpointKey, utc.Ticks);

    // Cap entries per stream message so a large backfill stays well under the gRPC message limit.
    private const int ChunkSize = 500;

    private async Task<bool> ShipAsync(IReadOnlyList<EventLogRecord> records, CancellationToken ct)
    {
        try
        {
            using var call = _connection.Client.StreamEvents(_connection.AuthHeaders(), cancellationToken: ct);
            for (var i = 0; i < records.Count; i += ChunkSize)
            {
                var batch = new EventBatch { HostId = _connection.HostId };
                foreach (var r in records.Skip(i).Take(ChunkSize))
                {
                    batch.Entries.Add(new EventEntry
                    {
                        Timestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(r.TimestampUtc, DateTimeKind.Utc)),
                        Channel = r.Channel,
                        Source = r.Source,
                        Level = r.Level,
                        EventId = r.EventId,
                        Message = r.Message,
                    });
                }
                await call.RequestStream.WriteAsync(batch, ct);
            }
            await call.RequestStream.CompleteAsync();
            await call.ResponseAsync;
            _logger.LogInformation("Shipped {Count} event-log records", records.Count);
            return true;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning("Event-log ship failed: {Status}; will retry", ex.StatusCode);
            return false;
        }
    }
}
