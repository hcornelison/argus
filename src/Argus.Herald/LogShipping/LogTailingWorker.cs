using Argus.Contracts;
using Argus.Herald.Configuration;
using Argus.Herald.Ingest;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using System.Text;

namespace Argus.Herald.LogShipping;

/// <summary>
/// Tails the configured log files/directories. Each cycle it discovers matching files,
/// reads any bytes appended since the persisted offset, splits them into lines, and ships
/// a LogBatch to styx. Offsets advance only after a successful ship.
/// </summary>
public class LogTailingWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IngestConnection _connection;
    private readonly HeraldOptions _options;
    private readonly CheckpointStore _checkpoints;
    private readonly ILogger<LogTailingWorker> _logger;

    public LogTailingWorker(IngestConnection connection, IOptions<HeraldOptions> options,
        CheckpointStore checkpoints, ILogger<LogTailingWorker> logger)
    {
        _connection = connection;
        _options = options.Value;
        _checkpoints = checkpoints;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                if (!await _connection.EnsureRegisteredAsync(ct)) continue;
                foreach (var file in DiscoverFiles())
                    await TailFileAsync(file, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Log tailing cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private IEnumerable<string> DiscoverFiles()
    {
        foreach (var path in _options.LogPaths)
        {
            if (Directory.Exists(path))
            {
                foreach (var f in Directory.EnumerateFiles(path, _options.LogFileFilter, SearchOption.AllDirectories))
                    yield return f;
            }
            else if (File.Exists(path))
            {
                yield return path;
            }
        }
    }

    private async Task TailFileAsync(string file, CancellationToken ct)
    {
        long length;
        try { length = new FileInfo(file).Length; }
        catch { return; }

        var offset = _checkpoints.Get(file);
        if (offset < 0)
        {
            // First time we see this file: start at the end so we only ship new lines.
            _checkpoints.Set(file, length);
            return;
        }

        if (length < offset)
        {
            // File was truncated/rotated; restart from the beginning.
            offset = 0;
        }
        if (length == offset) return;

        var lines = new List<LogLine>();
        long newOffset = offset;
        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            fs.Seek(offset, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                lines.Add(new LogLine
                {
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                    FilePath = file,
                    Line = line,
                    Level = string.Empty,
                });
            }
            newOffset = fs.Position;
        }

        if (lines.Count == 0) return;

        var batch = new LogBatch { HostId = _connection.HostId };
        batch.Lines.AddRange(lines);

        try
        {
            using var call = _connection.Client.StreamLogs(cancellationToken: ct);
            await call.RequestStream.WriteAsync(batch, ct);
            await call.RequestStream.CompleteAsync();
            await call.ResponseAsync;
            _checkpoints.Set(file, newOffset); // advance only after a successful ship
        }
        catch (RpcException ex)
        {
            _logger.LogWarning("Log ship failed for {File}: {Status}; will retry", file, ex.StatusCode);
        }
    }
}
