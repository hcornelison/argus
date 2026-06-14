using Argus.Codex;
using Argus.Codex.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Argus.Styx;

/// <summary>
/// Trims Redis Streams once per day. For each known host, calls XTRIM MINID on all four
/// streams to remove entries older than the configured retention window.
/// </summary>
public class RetentionBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly IServiceProvider _services;
    private readonly ILogger<RetentionBackgroundService> _logger;

    public RetentionBackgroundService(IServiceProvider services, ILogger<RetentionBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();
                var redis = scope.ServiceProvider.GetRequiredService<RedisStreamService>();
                var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<RetentionOptions>>();

                var cutoff = DateTime.UtcNow.AddDays(-options.CurrentValue.Days);
                var hostIds = await db.Hosts.Select(h => h.Id).ToListAsync(stoppingToken);

                foreach (var id in hostIds)
                    await redis.TrimStreamsForHostAsync(id, cutoff);

                _logger.LogInformation("Retention trim complete for {Count} host(s), cutoff {Cutoff:u}", hostIds.Count, cutoff);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retention trim failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
