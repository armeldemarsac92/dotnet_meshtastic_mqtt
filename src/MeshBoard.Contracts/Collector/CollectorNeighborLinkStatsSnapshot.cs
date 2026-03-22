namespace MeshBoard.Contracts.Collector;

public sealed class CollectorNeighborLinkStatsSnapshot
{
    public DateTimeOffset GeneratedAtUtc { get; set; }

    public string? ServerAddress { get; set; }

    public string? Region { get; set; }

    public string? ChannelName { get; set; }

    public string? SourceNodeId { get; set; }

    public string? TargetNodeId { get; set; }

    public int LookbackHours { get; set; }

    public int RowCount { get; set; }

    public IReadOnlyCollection<CollectorNeighborLinkHourlyRollup> Rollups { get; set; } = [];
}
