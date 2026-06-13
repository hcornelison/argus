using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using Argus.Herald.Configuration;

namespace Argus.Herald.Collectors;

/// <summary>Reads Windows Event Viewer channels via EventLogReader, filtered by time.</summary>
[SupportedOSPlatform("windows")]
public class WindowsEventLogCollector : IEventLogCollector
{
    private readonly EventLogOptions _options;
    private readonly ILogger<WindowsEventLogCollector> _logger;

    public WindowsEventLogCollector(EventLogOptions options, ILogger<WindowsEventLogCollector> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<IReadOnlyList<EventLogRecord>> CollectSinceAsync(DateTime sinceUtc, CancellationToken ct)
    {
        var results = new List<EventLogRecord>();
        // Query each channel for records created strictly after the checkpoint.
        var iso = sinceUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var xpath = $"*[System[TimeCreated[@SystemTime>'{iso}']]]";

        foreach (var channel in _options.Channels)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var query = new EventLogQuery(channel, PathType.LogName, xpath);
                using var reader = new EventLogReader(query);
                for (EventRecord? rec = reader.ReadEvent(); rec is not null; rec = reader.ReadEvent())
                {
                    using (rec)
                    {
                        var ts = (rec.TimeCreated ?? DateTime.UtcNow).ToUniversalTime();
                        string message;
                        try { message = rec.FormatDescription() ?? string.Empty; }
                        catch { message = string.Empty; }

                        results.Add(new EventLogRecord(
                            ts,
                            channel,
                            rec.ProviderName ?? string.Empty,
                            rec.LevelDisplayName ?? LevelName(rec.Level),
                            (int)(rec.Id),
                            message));
                    }
                }
            }
            catch (EventLogNotFoundException)
            {
                _logger.LogWarning("Event log channel '{Channel}' not found", channel);
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Access denied reading event log channel '{Channel}' (try running as admin)", channel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed reading event log channel '{Channel}'", channel);
            }
        }

        IReadOnlyList<EventLogRecord> ordered = results.OrderBy(r => r.TimestampUtc).ToList();
        return Task.FromResult(ordered);
    }

    private static string LevelName(byte? level) => level switch
    {
        1 => "Critical",
        2 => "Error",
        3 => "Warning",
        4 => "Information",
        5 => "Verbose",
        _ => "Information"
    };
}
