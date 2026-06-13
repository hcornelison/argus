using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Argus.Codex;

/// <summary>
/// Deletes time-series rows older than the configured retention window.
/// Invoked on a timer by styx's RetentionBackgroundService.
/// </summary>
public class RetentionService
{
    private readonly ArgusDbContext _db;
    private readonly IOptionsMonitor<RetentionOptions> _options;

    public RetentionService(ArgusDbContext db, IOptionsMonitor<RetentionOptions> options)
    {
        _db = db;
        _options = options;
    }

    public async Task<int> PurgeAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_options.CurrentValue.Days);
        var total = 0;

        total += await _db.MetricSamples.Where(x => x.TimestampUtc < cutoff).ExecuteDeleteAsync(ct);
        total += await _db.DiskSamples.Where(x => x.TimestampUtc < cutoff).ExecuteDeleteAsync(ct);
        total += await _db.ProcessSamples.Where(x => x.TimestampUtc < cutoff).ExecuteDeleteAsync(ct);
        total += await _db.EventLogEntries.Where(x => x.TimestampUtc < cutoff).ExecuteDeleteAsync(ct);
        total += await _db.LogEntries.Where(x => x.TimestampUtc < cutoff).ExecuteDeleteAsync(ct);

        return total;
    }
}
