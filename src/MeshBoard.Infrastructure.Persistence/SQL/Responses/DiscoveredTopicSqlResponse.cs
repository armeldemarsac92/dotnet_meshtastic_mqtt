namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class DiscoveredTopicSqlResponse
{
    public required string Channel { get; set; }

    public required string Region { get; set; }

    public required string TopicPattern { get; set; }
}
