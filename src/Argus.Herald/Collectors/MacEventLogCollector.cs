using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Argus.Herald.Configuration;

namespace Argus.Herald.Collectors;

/// <summary>Reads the macOS unified log via `log show --style ndjson`, filtered by time and type.</summary>
[SupportedOSPlatform("osx")]
public class MacEventLogCollector : IEventLogCollector
{
    private readonly EventLogOptions _options;

    public MacEventLogCollector(EventLogOptions options) => _options = options;

    public async Task<IReadOnlyList<EventLogRecord>> CollectSinceAsync(DateTime sinceUtc, CancellationToken ct)
    {
        // log show --start interprets local time.
        var start = sinceUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        var args = new List<string> { "show", "--start", start, "--style", "ndjson", "--predicate", Predicate(_options.MinLevel) };
        // error/fault are always captured; info/debug need explicit flags.
        if (_options.MinLevel is "Information") args.Add("--info");
        else if (_options.MinLevel is "Verbose") { args.Add("--info"); args.Add("--debug"); }

        var output = await CommandRunner.RunAsync("log", args, ct);
        if (string.IsNullOrEmpty(output)) return Array.Empty<EventLogRecord>();

        var results = new List<EventLogRecord>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line[0] != '{') continue; // skip any non-JSON preamble
            var rec = TryParse(line);
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
            if (root.TryGetProperty("timestamp", out var tsEl) && tsEl.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(tsEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            {
                ts = dto.UtcDateTime;
            }

            var subsystem = GetString(root, "subsystem");
            var category = GetString(root, "category");
            var channel = string.Join(":", new[] { subsystem, category }.Where(s => !string.IsNullOrEmpty(s)));
            if (string.IsNullOrEmpty(channel)) channel = "unified-log";

            var source = GetString(root, "process") ?? GetString(root, "processImagePath") ?? string.Empty;
            var level = LevelFromType(GetString(root, "messageType"));
            var message = GetString(root, "eventMessage") ?? string.Empty;

            return new EventLogRecord(ts, channel, source, level, 0, message);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static string Predicate(string minLevel) => minLevel switch
    {
        "Critical" => "messageType == \"fault\"",
        "Information" or "Verbose" => "messageType == \"default\" OR messageType == \"info\" OR messageType == \"error\" OR messageType == \"fault\"",
        _ => "messageType == \"error\" OR messageType == \"fault\"" // Error / Warning -> bounded
    };

    private static string LevelFromType(string? type) => type?.ToLowerInvariant() switch
    {
        "fault" => "Critical",
        "error" => "Error",
        "default" => "Information",
        "info" => "Information",
        "debug" => "Verbose",
        _ => "Information"
    };
}
