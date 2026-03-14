namespace MeshBoard.Client.Maps;

public sealed record MapProjectionSnapshot
{
    public static readonly IReadOnlyList<MapProjectionEnvelope> EmptyNodes = Array.Empty<MapProjectionEnvelope>();

    public static readonly IReadOnlyList<MapNodeActivity> EmptyActivityPulses = Array.Empty<MapNodeActivity>();

    public IReadOnlyList<MapProjectionEnvelope> Nodes { get; init; } = EmptyNodes;

    public DateTimeOffset? LastProjectedAtUtc { get; init; }

    public long TotalProjected { get; init; }
}
