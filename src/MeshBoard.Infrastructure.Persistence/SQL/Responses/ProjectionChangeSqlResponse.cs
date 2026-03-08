namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class ProjectionChangeSqlResponse
{
    public string? EntityKey { get; set; }

    public long Id { get; set; }

    public string WorkspaceId { get; set; } = string.Empty;

    public string ChangeKind { get; set; } = string.Empty;

    public string OccurredAtUtc { get; set; } = string.Empty;
}
