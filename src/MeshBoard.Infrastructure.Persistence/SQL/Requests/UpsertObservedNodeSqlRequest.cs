namespace MeshBoard.Infrastructure.Persistence.SQL.Requests;

internal sealed class UpsertObservedNodeSqlRequest
{
    public required string NodeId { get; set; }

    public string? ShortName { get; set; }

    public string? LongName { get; set; }

    public string? LastHeardAtUtc { get; set; }

    public string? LastTextMessageAtUtc { get; set; }

    public double? LastKnownLatitude { get; set; }

    public double? LastKnownLongitude { get; set; }
}
