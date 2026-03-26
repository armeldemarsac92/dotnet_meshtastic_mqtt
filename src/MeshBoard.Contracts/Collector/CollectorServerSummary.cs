namespace MeshBoard.Contracts.Collector;

public sealed class CollectorServerSummary
{
    public string ServerAddress { get; set; } = string.Empty;

    public DateTimeOffset FirstObservedAtUtc { get; set; }

    public DateTimeOffset LastObservedAtUtc { get; set; }

    public int ChannelCount { get; set; }

    public int NodeCount { get; set; }

    public int MessageCount { get; set; }

    public int NeighborLinkCount { get; set; }
}
