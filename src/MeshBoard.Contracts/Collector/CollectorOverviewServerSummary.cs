namespace MeshBoard.Contracts.Collector;

public sealed class CollectorOverviewServerSummary
{
    public string ServerAddress { get; set; } = string.Empty;

    public DateTimeOffset FirstObservedAtUtc { get; set; }

    public DateTimeOffset LastObservedAtUtc { get; set; }

    public int ChannelCount { get; set; }

    public int ActiveNodeCount { get; set; }

    public int ActiveLinkCount { get; set; }

    public int PacketCountInLookback { get; set; }

    public int NeighborObservationCountInLookback { get; set; }

    public IReadOnlyCollection<CollectorOverviewChannelSummary> Channels { get; set; } = [];
}
