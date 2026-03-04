namespace MeshBoard.Contracts.Nodes;

public sealed class UpsertObservedNodeRequest
{
    public required string NodeId { get; set; }

    public string? ShortName { get; set; }

    public string? LongName { get; set; }

    public DateTimeOffset? LastHeardAtUtc { get; set; }

    public DateTimeOffset? LastTextMessageAtUtc { get; set; }

    public double? LastKnownLatitude { get; set; }

    public double? LastKnownLongitude { get; set; }
}
