namespace MeshBoard.Contracts.Meshtastic;

public sealed class MeshtasticEnvelope
{
    public string Topic { get; set; } = string.Empty;

    public string PacketType { get; set; } = string.Empty;

    public uint? PacketId { get; set; }

    public string PayloadPreview { get; set; } = string.Empty;

    public string? FromNodeId { get; set; }

    public string? ToNodeId { get; set; }

    public bool IsPrivate { get; set; }

    public string? ShortName { get; set; }

    public string? LongName { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
