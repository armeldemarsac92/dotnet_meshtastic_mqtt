namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class CollectorMapLinkSqlResponse
{
    public required string SourceNodeId { get; set; }

    public required string TargetNodeId { get; set; }

    public float? SnrDb { get; set; }

    public required string LastSeenAtUtc { get; set; }

    public required string ServerAddress { get; set; }

    public required string Region { get; set; }

    public required string MeshVersion { get; set; }

    public required string ChannelName { get; set; }
}
