namespace MeshBoard.Client.Nodes;

public sealed record NodeProjectionSnapshot
{
    public static readonly IReadOnlyList<NodeProjectionEnvelope> EmptyNodes = Array.Empty<NodeProjectionEnvelope>();

    public IReadOnlyList<NodeProjectionEnvelope> Nodes { get; init; } = EmptyNodes;

    public DateTimeOffset? LastProjectedAtUtc { get; init; }

    public long TotalProjected { get; init; }
}
