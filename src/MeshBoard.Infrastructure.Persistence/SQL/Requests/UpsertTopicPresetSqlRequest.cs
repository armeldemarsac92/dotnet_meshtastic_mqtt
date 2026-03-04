namespace MeshBoard.Infrastructure.Persistence.SQL.Requests;

internal sealed class UpsertTopicPresetSqlRequest
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string TopicPattern { get; set; }

    public int IsDefault { get; set; }

    public required string CreatedAtUtc { get; set; }
}
