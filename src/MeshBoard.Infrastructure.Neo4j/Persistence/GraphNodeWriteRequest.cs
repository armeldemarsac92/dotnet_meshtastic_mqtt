namespace MeshBoard.Infrastructure.Neo4j.Persistence;

public sealed class GraphNodeWriteRequest
{
    public required string BrokerServer { get; init; }

    public required string NodeId { get; init; }

    public string? ShortName { get; init; }

    public string? LongName { get; init; }

    public DateTimeOffset? LastHeardAtUtc { get; init; }

    public DateTimeOffset? LastTextMessageAtUtc { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? ChannelTopicPattern { get; init; }

    public DateTimeOffset ObservedAtUtc { get; init; }
}
