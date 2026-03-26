namespace MeshBoard.Contracts.Collector;

public sealed class CollectorOverviewSnapshot
{
    public DateTimeOffset GeneratedAtUtc { get; set; }

    public string? ServerAddress { get; set; }

    public string? Region { get; set; }

    public string? ChannelName { get; set; }

    public int ActiveWithinHours { get; set; }

    public int LookbackHours { get; set; }

    public int ServerCount { get; set; }

    public int ChannelCount { get; set; }

    public int ActiveNodeCount { get; set; }

    public int ActiveLinkCount { get; set; }

    public int PacketCountInLookback { get; set; }

    public int NeighborObservationCountInLookback { get; set; }

    public IReadOnlyCollection<CollectorOverviewServerSummary> Servers { get; set; } = [];
}
