namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class NodeSqlResponse
{
    public required string NodeId { get; set; }

    public string? ShortName { get; set; }

    public string? LongName { get; set; }

    public string? LastHeardAtUtc { get; set; }

    public string? LastTextMessageAtUtc { get; set; }

    public double? LastKnownLatitude { get; set; }

    public double? LastKnownLongitude { get; set; }
}
