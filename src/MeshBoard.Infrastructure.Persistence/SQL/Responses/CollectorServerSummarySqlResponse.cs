namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class CollectorServerSummarySqlResponse
{
    public required string ServerAddress { get; set; }

    public required string FirstObservedAtUtc { get; set; }

    public required string LastObservedAtUtc { get; set; }

    public int ChannelCount { get; set; }

    public int NodeCount { get; set; }

    public int MessageCount { get; set; }

    public int NeighborLinkCount { get; set; }
}
