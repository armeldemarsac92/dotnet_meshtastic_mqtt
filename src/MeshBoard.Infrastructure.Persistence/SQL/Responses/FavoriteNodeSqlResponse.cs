namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class FavoriteNodeSqlResponse
{
    public required string Id { get; set; }

    public required string NodeId { get; set; }

    public string? ShortName { get; set; }

    public string? LongName { get; set; }

    public required string CreatedAtUtc { get; set; }
}
