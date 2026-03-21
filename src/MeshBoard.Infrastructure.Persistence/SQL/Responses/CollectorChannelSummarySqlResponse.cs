namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class CollectorChannelSummarySqlResponse
{
    public required string ServerAddress { get; set; }

    public required string Region { get; set; }

    public required string MeshVersion { get; set; }

    public required string ChannelName { get; set; }

    public required string TopicPattern { get; set; }

    public required string FirstObservedAtUtc { get; set; }

    public required string LastObservedAtUtc { get; set; }

    public int NodeCount { get; set; }

    public int MessageCount { get; set; }

    public int NeighborLinkCount { get; set; }
}
