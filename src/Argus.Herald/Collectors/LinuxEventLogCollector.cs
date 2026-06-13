using System.Runtime.Versioning;
using System.Text.Json;
using Argus.Herald.Configuration;

namespace Argus.Herald.Collectors;

/// <summary>Reads the systemd journal via `journalctl -o json`, filtered by time and priority.</summary>
[SupportedOSPlatform("linux")]
public class LinuxEventLogCollector : IEventLogCollector
{
    private readonly EventLogOptions _options;

    public LinuxEventLogCollector(EventLogOptions options) => _options = options;

    public async Task<IReadOnlyList<EventLogRecord>> CollectSinceAsync(DateTime sinceUtc, CancellationToken ct)
    {
        // --since is interpreted in local time; __REALTIME_TIMESTAMP we parse is absolute epoch.
        var since = sinceUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        var maxPriority = MaxPriority(_options.MinLevel);

        var args = new[] { "--since", since, "-o", "json", "--no-pager", "-p", maxPriority.ToString() };
        var output = await CommandRunner.RunAsync("journalctl", args, ct);
        if (string.IsNullOrEmpty(output)) return Array.Empty<EventLogRecord>();

        var results = new List<EventLogRecord>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            EventLogRecord? rec = TryParse(line);
            if (rec is not null) results.Add(rec);
        }
        return results.OrderBy(r => r.TimestampUtc).ToList();
    }

    private static EventLogRecord? TryParse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var ts = DateTime.UtcNow;
            if (root.TryGetProperty("__REALTIME_TIMESTAMP", out var tsEl) &&
                long.TryParse(tsEl.GetString(), out var micros))
            {
                ts = DateTimeOffset.FromUnixTimeMilliseconds(micros / 1000).UtcDateTime;
            }

            var channel = GetString(root, "_SYSTEMD_UNIT") ?? GetString(root, "SYSLOG_IDENTIFIER") ?? "journal";
            var source = GetString(root, "SYSLOG_IDENTIFIER") ?? GetString(root, "_COMM") ?? string.Empty;
            var priority = GetString(root, "PRIORITY");
            var level = LevelFromPriority(priority);
            var message = GetMessage(root);

            return new EventLogRecord(ts, channel, source, level, 0, message);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static string GetMessage(JsonElement root)
    {
        if (!root.TryGetProperty("MESSAGE", out var el)) return string.Empty;
        if (el.ValueKind == JsonValueKind.String) return el.GetString() ?? string.Empty;
        // journald sometimes encodes non-UTF8 messages as a byte array.
        if (el.ValueKind == JsonValueKind.Array)
        {
            var bytes = el.EnumerateArray().Select(b => (byte)b.GetInt32()).ToArray();
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        return string.Empty;
    }

    private static int MaxPriority(string minLevel) => minLevel switch
    {
        "Critical" => 2,
        "Error" => 3,
        "Warning" => 4,
        "Information" => 6,
        _ => 7 // Verbose / unknown
    };

    private static string LevelFromPriority(string? priority) => priority switch
    {
        "0" or "1" or "2" => "Critical",
        "3" => "Error",
        "4" => "Warning",
        "5" or "6" => "Information",
        _ => "Verbose"
    };
}
