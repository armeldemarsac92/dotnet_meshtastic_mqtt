namespace MeshBoard.Client.Maps;

public sealed record MapProjectionSnapshot
{
    public static readonly IReadOnlyList<MapNodePoint> EmptyNodes = Array.Empty<MapNodePoint>();

    public static readonly IReadOnlyList<MapNodeActivity> EmptyActivityPulses = Array.Empty<MapNodeActivity>();

    public IReadOnlyList<MapNodePoint> Nodes { get; init; } = EmptyNodes;

    public IReadOnlyList<MapNodeActivity> ActivityPulses { get; init; } = EmptyActivityPulses;

    public DateTimeOffset? LastProjectedAtUtc { get; init; }

    public long TotalProjected { get; init; }
}
