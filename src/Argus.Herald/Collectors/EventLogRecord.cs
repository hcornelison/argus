namespace Argus.Herald.Collectors;

public record EventLogRecord(
    DateTime TimestampUtc,
    string Channel,
    string Source,
    string Level,
    int EventId,
    string Message);

public interface IEventLogCollector
{
    /// <summary>
    /// Returns OS event-log records newer than <paramref name="sinceUtc"/>, oldest first.
    /// Implementations should be resilient to a missing log source and return an empty list.
    /// </summary>
    Task<IReadOnlyList<EventLogRecord>> CollectSinceAsync(DateTime sinceUtc, CancellationToken ct);
}
