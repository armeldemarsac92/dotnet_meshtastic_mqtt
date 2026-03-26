namespace MeshBoard.Contracts.Meshtastic;

public sealed class MeshtasticTracerouteHop
{
    public string NodeId { get; set; } = string.Empty;

    public float? SnrDb { get; set; }
}
