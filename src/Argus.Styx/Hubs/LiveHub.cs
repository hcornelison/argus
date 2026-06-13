using Microsoft.AspNetCore.SignalR;

namespace Argus.Styx.Hubs;

/// <summary>
/// Live push channel to pantheon. Clients join a per-host group ("host-{id}")
/// to receive that host's new metrics, process snapshots, and log lines.
/// </summary>
public class LiveHub : Hub
{
    public static string GroupFor(long hostId) => $"host-{hostId}";

    public Task Subscribe(long hostId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(hostId));

    public Task Unsubscribe(long hostId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(hostId));
}
