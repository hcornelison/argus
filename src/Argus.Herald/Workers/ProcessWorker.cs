using Argus.Contracts;
using Argus.Herald.Collectors;
using Argus.Herald.Configuration;
using Argus.Herald.Ingest;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Argus.Herald.Workers;

/// <summary>Captures a per-process snapshot on a timer and streams it to styx.</summary>
public class ProcessWorker : BackgroundService
{
    private readonly ProcessCollector _collector;
    private readonly IngestConnection _connection;
    private readonly HeraldOptions _options;
    private readonly ILogger<ProcessWorker> _logger;

    public ProcessWorker(ProcessCollector collector, IngestConnection connection,
        IOptions<HeraldOptions> options, ILogger<ProcessWorker> logger)
    {
        _collector = collector;
        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.ProcessIntervalSeconds)));
        // Prime the CPU baseline so the first reported snapshot has meaningful values.
        _collector.Collect(_options.MaxProcesses);

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                if (!await _connection.EnsureRegisteredAsync(ct)) continue;

                var batch = new ProcessBatch
                {
                    HostId = _connection.HostId,
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                };
                batch.Processes.AddRange(_collector.Collect(_options.MaxProcesses).Select(p => new ProcessSample
                {
                    Pid = p.Pid, Name = p.Name, CpuPercent = p.CpuPercent,
                    MemoryBytes = p.MemoryBytes, ThreadCount = p.ThreadCount,
                }));

                using var call = _connection.Client.StreamProcesses(_connection.AuthHeaders(), cancellationToken: ct);
                await call.RequestStream.WriteAsync(batch, ct);
                await call.RequestStream.CompleteAsync();
                await call.ResponseAsync;
            }
            catch (OperationCanceledException) { break; }
            catch (RpcException ex)
            {
                _logger.LogWarning("Process ship failed: {Status}", ex.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Process collection failed");
            }
        }
    }
}
