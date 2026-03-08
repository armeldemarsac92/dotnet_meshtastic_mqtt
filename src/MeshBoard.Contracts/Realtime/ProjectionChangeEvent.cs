namespace MeshBoard.Contracts.Realtime;

public sealed class ProjectionChangeEvent
{
    public string? EntityKey { get; set; }

    public long Id { get; set; }

    public string WorkspaceId { get; set; } = string.Empty;

    public ProjectionChangeKind Kind { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}
