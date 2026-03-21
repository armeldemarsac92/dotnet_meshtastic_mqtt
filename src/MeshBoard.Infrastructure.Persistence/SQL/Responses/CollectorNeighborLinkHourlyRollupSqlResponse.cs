namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class CollectorNeighborLinkHourlyRollupSqlResponse
{
    public required string BucketStartUtc { get; set; }

    public required string ServerAddress { get; set; }

    public required string Region { get; set; }

    public required string MeshVersion { get; set; }

    public required string ChannelName { get; set; }

    public required string SourceNodeId { get; set; }

    public required string TargetNodeId { get; set; }

    public string? SourceShortName { get; set; }

    public string? SourceLongName { get; set; }

    public string? TargetShortName { get; set; }

    public string? TargetLongName { get; set; }

    public int ObservationCount { get; set; }

    public float? AverageSnrDb { get; set; }

    public float? MaxSnrDb { get; set; }

    public float? LastSnrDb { get; set; }

    public required string FirstSeenAtUtc { get; set; }

    public required string LastSeenAtUtc { get; set; }
}
