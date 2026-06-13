using Argus.Contracts;
using Argus.Herald.Collectors;
using Argus.Herald.Configuration;
using Argus.Herald.Ingest;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Argus.Herald.Workers;

/// <summary>Samples overall resources on a timer and streams them to styx.</summary>
public class MetricsWorker : BackgroundService
{
    private readonly IResourceCollector _collector;
    private readonly IngestConnection _connection;
    private readonly HeraldOptions _options;
    private readonly ILogger<MetricsWorker> _logger;
    private readonly Queue<MetricBatch> _buffer = new();

    public MetricsWorker(IResourceCollector collector, IngestConnection connection,
        IOptions<HeraldOptions> options, ILogger<MetricsWorker> logger)
    {
        _collector = collector;
        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.MetricsIntervalSeconds)));
        do
        {
            try
            {
                if (!await _connection.EnsureRegisteredAsync(ct)) continue;

                var snap = await _collector.CollectAsync(ct);
                var sample = new MetricSample
                {
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                    CpuPercent = snap.CpuPercent,
                    MemoryTotalBytes = snap.MemoryTotalBytes,
                    MemoryUsedBytes = snap.MemoryUsedBytes,
                };
                sample.Disks.AddRange(snap.Disks.Select(d => new Argus.Contracts.DiskSample
                {
                    Mount = d.Mount, TotalBytes = d.TotalBytes, UsedBytes = d.UsedBytes,
                }));

                var batch = new MetricBatch { HostId = _connection.HostId };
                batch.Samples.Add(sample);
                Enqueue(batch);
                await FlushAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Metrics collection/ship failed; will retry");
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private void Enqueue(MetricBatch batch)
    {
        _buffer.Enqueue(batch);
        while (_buffer.Count > _options.MaxBufferedBatches) _buffer.Dequeue();
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        if (_buffer.Count == 0) return;
        try
        {
            using var call = _connection.Client.StreamMetrics(_connection.AuthHeaders(), cancellationToken: ct);
            // Snapshot the count so a write failure leaves un-sent batches buffered.
            var pending = _buffer.Count;
            for (var i = 0; i < pending; i++)
            {
                await call.RequestStream.WriteAsync(_buffer.Peek(), ct);
                _buffer.Dequeue();
            }
            await call.RequestStream.CompleteAsync();
            await call.ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning("Metrics ship failed ({Status}); buffered {Count} batches", ex.StatusCode, _buffer.Count);
        }
    }
}
