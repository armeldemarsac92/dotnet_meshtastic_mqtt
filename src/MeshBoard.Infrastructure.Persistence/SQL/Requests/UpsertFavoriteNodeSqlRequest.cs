namespace MeshBoard.Infrastructure.Persistence.SQL.Requests;

internal sealed class UpsertFavoriteNodeSqlRequest
{
    public required string Id { get; set; }

    public required string NodeId { get; set; }

    public string? ShortName { get; set; }

    public string? LongName { get; set; }

    public required string CreatedAtUtc { get; set; }
}
