using Argus.Codex;

namespace Argus.Styx;

/// <summary>Runs the retention purge shortly after startup and once every 24h thereafter.</summary>
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
        // Small delay so migrations/startup can settle first.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _services.CreateScope();
                var retention = scope.ServiceProvider.GetRequiredService<RetentionService>();
                var deleted = await retention.PurgeAsync(stoppingToken);
                _logger.LogInformation("Retention purge removed {Count} rows", deleted);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retention purge failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
