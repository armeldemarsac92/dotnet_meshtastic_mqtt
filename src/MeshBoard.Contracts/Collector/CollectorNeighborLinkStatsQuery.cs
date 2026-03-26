namespace MeshBoard.Contracts.Collector;

public sealed class CollectorNeighborLinkStatsQuery
{
    public string? ServerAddress { get; set; }

    public string? Region { get; set; }

    public string? ChannelName { get; set; }

    public string? SourceNodeId { get; set; }

    public string? TargetNodeId { get; set; }

    public int LookbackHours { get; set; } = 24 * 7;

    public int MaxRows { get; set; } = 500;
}
