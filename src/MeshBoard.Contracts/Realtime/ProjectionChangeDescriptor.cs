namespace MeshBoard.Contracts.Realtime;

public sealed class ProjectionChangeDescriptor
{
    public string? EntityKey { get; init; }

    public ProjectionChangeKind Kind { get; init; }
}
