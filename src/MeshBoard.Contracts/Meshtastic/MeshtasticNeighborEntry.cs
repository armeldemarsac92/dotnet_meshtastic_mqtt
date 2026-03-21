namespace MeshBoard.Contracts.Meshtastic;

public sealed class MeshtasticNeighborEntry
{
    public string NodeId { get; set; } = string.Empty;

    public float? SnrDb { get; set; }

    public DateTimeOffset? LastRxAtUtc { get; set; }
}
