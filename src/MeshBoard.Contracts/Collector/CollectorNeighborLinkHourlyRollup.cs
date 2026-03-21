namespace MeshBoard.Contracts.Collector;

public sealed class CollectorNeighborLinkHourlyRollup
{
    public DateTimeOffset BucketStartUtc { get; set; }

    public string ServerAddress { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string MeshVersion { get; set; } = string.Empty;

    public string ChannelName { get; set; } = string.Empty;

    public string SourceNodeId { get; set; } = string.Empty;

    public string TargetNodeId { get; set; } = string.Empty;

    public string? SourceShortName { get; set; }

    public string? SourceLongName { get; set; }

    public string? TargetShortName { get; set; }

    public string? TargetLongName { get; set; }

    public int ObservationCount { get; set; }

    public float? AverageSnrDb { get; set; }

    public float? MaxSnrDb { get; set; }

    public float? LastSnrDb { get; set; }

    public DateTimeOffset FirstSeenAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }
}
