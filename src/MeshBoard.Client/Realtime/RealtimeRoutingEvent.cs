namespace MeshBoard.Client.Realtime;

public sealed class RealtimeRoutingEvent
{
    public string Kind { get; init; } = string.Empty;

    public int? ErrorCode { get; init; }

    public string? ErrorName { get; init; }

    public IReadOnlyList<string> RouteNodeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<int> SnrTowards { get; init; } = Array.Empty<int>();

    public IReadOnlyList<string> RouteBackNodeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<int> SnrBack { get; init; } = Array.Empty<int>();
}
