namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class TopicPresetSqlResponse
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string TopicPattern { get; set; }

    public int IsDefault { get; set; }

    public required string CreatedAtUtc { get; set; }
}
