namespace MeshBoard.Infrastructure.Persistence.SQL.Requests;

internal sealed class UpsertNeighborLinkSqlRequest
{
    public required string WorkspaceId { get; set; }

    public required string SourceNodeId { get; set; }

    public required string TargetNodeId { get; set; }

    public float? SnrDb { get; set; }

    public required string LastSeenAtUtc { get; set; }
}
