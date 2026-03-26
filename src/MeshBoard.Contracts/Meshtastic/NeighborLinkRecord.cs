namespace MeshBoard.Contracts.Meshtastic;

public sealed class NeighborLinkRecord
{
    public string SourceNodeId { get; set; } = string.Empty;

    public string TargetNodeId { get; set; } = string.Empty;

    public float? SnrDb { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }
}
