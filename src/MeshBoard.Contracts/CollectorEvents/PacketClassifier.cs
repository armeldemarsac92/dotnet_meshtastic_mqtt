namespace MeshBoard.Contracts.CollectorEvents;

public static class PacketClassifier
{
    private static readonly HashSet<string> SupportedPacketTypes =
    [
        "Text Message",
        "Compressed Text Message",
        "Position Update",
        "Node Info",
        "Routing",
        "Telemetry",
        "Traceroute",
        "Neighbor Info"
    ];

    public static CollectorDecodeStatus ResolveDecodeStatus(string? packetType)
    {
        if (string.Equals(packetType, "Encrypted Packet", StringComparison.Ordinal) ||
            string.Equals(packetType, "Unknown Packet", StringComparison.Ordinal))
        {
            return CollectorDecodeStatus.Failed;
        }

        return packetType is not null && SupportedPacketTypes.Contains(packetType)
            ? CollectorDecodeStatus.Succeeded
            : CollectorDecodeStatus.UnsupportedPayload;
    }

    public static CollectorDecryptStatus ResolveDecryptStatus(string? packetType)
    {
        return string.Equals(packetType, "Encrypted Packet", StringComparison.Ordinal)
            ? CollectorDecryptStatus.Failed
            : CollectorDecryptStatus.NotRequired;
    }

    public static CollectorLinkOrigin ResolveLinkOrigin(bool hasNeighbors, bool hasMultiHopTraceroute)
    {
        if (hasNeighbors)
        {
            return CollectorLinkOrigin.NeighborInfo;
        }

        if (hasMultiHopTraceroute)
        {
            return CollectorLinkOrigin.Traceroute;
        }

        return CollectorLinkOrigin.MeshPacket;
    }
}
