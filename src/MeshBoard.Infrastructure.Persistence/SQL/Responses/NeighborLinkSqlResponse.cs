namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class NeighborLinkSqlResponse
{
    public required string SourceNodeId { get; set; }

    public required string TargetNodeId { get; set; }

    public float? SnrDb { get; set; }

    public required string LastSeenAtUtc { get; set; }
}
