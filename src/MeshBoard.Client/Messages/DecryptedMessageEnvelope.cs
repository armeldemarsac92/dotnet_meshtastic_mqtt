using MeshBoard.Client.Realtime;

namespace MeshBoard.Client.Messages;

public sealed record DecryptedMessageEnvelope
{
    public string Id { get; init; } = string.Empty;

    public string WorkspaceId { get; init; } = string.Empty;

    public string BrokerServer { get; init; } = string.Empty;

    public string SourceTopic { get; init; } = string.Empty;

    public string DownstreamTopic { get; init; } = string.Empty;

    public DateTimeOffset ReceivedAtUtc { get; init; }

    public bool IsEncrypted { get; init; }

    public string DecryptResultClassification { get; init; } = RealtimePacketWorkerDecryptResultClassifications.NotAttempted;

    public uint? PacketId { get; init; }

    public uint? FromNodeNumber { get; init; }

    public int PortNumValue { get; init; }

    public string PortNumName { get; init; } = string.Empty;

    public string PacketType { get; init; } = string.Empty;

    public string PayloadPreview { get; init; } = string.Empty;

    public string PayloadBase64 { get; init; } = string.Empty;

    public int PayloadSizeBytes { get; init; }

    public uint? SourceNodeNumber { get; init; }

    public uint? DestinationNodeNumber { get; init; }

    public float? RxSnr { get; init; }

    public int? RxRssi { get; init; }

    public int? HopLimit { get; init; }

    public int? HopStart { get; init; }

    public string? GatewayNodeId { get; init; }

    public RealtimeRoutingEvent? TracerouteInfo { get; init; }
}
