namespace MeshBoard.Client.Realtime;

public sealed class RealtimeDecodedPacketEvent
{
    public int PortNumValue { get; init; }

    public string PortNumName { get; init; } = string.Empty;

    public string PacketType { get; init; } = string.Empty;

    public string PayloadBase64 { get; init; } = string.Empty;

    public int PayloadSizeBytes { get; init; }

    public string PayloadPreview { get; init; } = string.Empty;

    public uint? SourceNodeNumber { get; init; }

    public uint? DestinationNodeNumber { get; init; }

    public RealtimeNodeProjectionEvent? NodeProjection { get; init; }

    public RealtimeNeighborInfoEvent? NeighborInfo { get; init; }

    public RealtimeRoutingEvent? RoutingInfo { get; init; }

    public RealtimeRoutingEvent? TracerouteInfo { get; init; }
}
