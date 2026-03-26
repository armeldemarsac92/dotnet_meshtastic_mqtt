namespace MeshBoard.Infrastructure.Neo4j.Repositories;

public sealed class GraphLinkWriteRequest
{
    public required string BrokerServer { get; init; }

    public required string ChannelKey { get; init; }

    public required string TopicPattern { get; init; }

    public required string SourceNodeId { get; init; }

    public required string TargetNodeId { get; init; }

    public DateTimeOffset ObservedAtUtc { get; init; }

    public float? SnrDb { get; init; }

    public required string LinkOrigin { get; init; }
}
