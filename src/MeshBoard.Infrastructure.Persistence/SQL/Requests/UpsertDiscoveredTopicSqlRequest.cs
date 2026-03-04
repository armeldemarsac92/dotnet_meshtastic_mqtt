namespace MeshBoard.Infrastructure.Persistence.SQL.Requests;

internal sealed class UpsertDiscoveredTopicSqlRequest
{
    public required string Channel { get; set; }

    public required string ObservedAtUtc { get; set; }

    public required string Region { get; set; }

    public required string TopicPattern { get; set; }
}
