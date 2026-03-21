namespace MeshBoard.Client.Realtime;

public sealed class RealtimeNeighborInfoEvent
{
    public string ReportingNodeId { get; init; } = string.Empty;

    public IReadOnlyList<RealtimeNeighborEntry> Neighbors { get; init; } = Array.Empty<RealtimeNeighborEntry>();
}

public sealed class RealtimeNeighborEntry
{
    public string NodeId { get; init; } = string.Empty;

    public float? SnrDb { get; init; }

    public DateTimeOffset? LastRxAtUtc { get; init; }
}
