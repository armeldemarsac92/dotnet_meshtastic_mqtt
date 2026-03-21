namespace MeshBoard.Contracts.Collector;

public sealed class CollectorOverviewChannelSummary
{
    public string Region { get; set; } = string.Empty;

    public string MeshVersion { get; set; } = string.Empty;

    public string ChannelName { get; set; } = string.Empty;

    public string TopicPattern { get; set; } = string.Empty;

    public DateTimeOffset FirstObservedAtUtc { get; set; }

    public DateTimeOffset LastObservedAtUtc { get; set; }

    public int ActiveNodeCount { get; set; }

    public int ActivePositionedNodeCount { get; set; }

    public int ActiveLinkCount { get; set; }

    public int ConnectedComponentCount { get; set; }

    public int LargestConnectedComponentSize { get; set; }

    public int IsolatedNodeCount { get; set; }

    public int BridgeNodeCount { get; set; }

    public int PacketCountInLookback { get; set; }

    public int NeighborObservationCountInLookback { get; set; }

    public IReadOnlyCollection<CollectorOverviewPacketTypeSummary> TopPacketTypes { get; set; } = [];
}
