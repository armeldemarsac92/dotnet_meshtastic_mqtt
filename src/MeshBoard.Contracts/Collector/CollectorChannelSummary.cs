namespace MeshBoard.Contracts.Collector;

public sealed class CollectorChannelSummary
{
    public string ServerAddress { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string MeshVersion { get; set; } = string.Empty;

    public string ChannelName { get; set; } = string.Empty;

    public string TopicPattern { get; set; } = string.Empty;

    public DateTimeOffset FirstObservedAtUtc { get; set; }

    public DateTimeOffset LastObservedAtUtc { get; set; }

    public int NodeCount { get; set; }

    public int MessageCount { get; set; }

    public int NeighborLinkCount { get; set; }
}
