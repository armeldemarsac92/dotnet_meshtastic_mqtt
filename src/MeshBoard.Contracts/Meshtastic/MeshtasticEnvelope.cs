namespace MeshBoard.Contracts.Meshtastic;

public sealed class MeshtasticEnvelope
{
    public string Topic { get; set; } = string.Empty;

    public string PacketType { get; set; } = string.Empty;

    public string PayloadPreview { get; set; } = string.Empty;

    public string? FromNodeId { get; set; }

    public string? ToNodeId { get; set; }

    public bool IsPrivate { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
