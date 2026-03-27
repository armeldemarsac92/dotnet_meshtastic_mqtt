using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Contracts.CollectorEvents.Normalized;

public sealed class PacketNormalized : CollectorEventMetadata
{
    public string Topic { get; init; } = string.Empty;

    public string TopicPattern { get; init; } = string.Empty;

    public string PacketKey { get; init; } = string.Empty;

    public string? Region { get; init; }

    public string? ChannelName { get; init; }

    public string? MeshVersion { get; init; }

    public DateTimeOffset ReceivedAtUtc { get; init; }

    public DateTimeOffset ObservedAtUtc { get; init; }

    public uint? PacketId { get; init; }

    public string PacketType { get; init; } = string.Empty;

    public string PayloadPreview { get; init; } = string.Empty;

    public string? FromNodeId { get; init; }

    public string? ToNodeId { get; init; }

    public string? GatewayNodeId { get; init; }

    public bool IsPrivate { get; init; }

    public CollectorDecodeStatus DecodeStatus { get; init; }

    public CollectorDecryptStatus DecryptStatus { get; init; }

    public string? ShortName { get; init; }

    public string? LongName { get; init; }

    public string? LastHeardChannel { get; init; }

    public float? RxSnr { get; init; }

    public int? RxRssi { get; init; }

    public uint? HopLimit { get; init; }

    public uint? HopStart { get; init; }

    public IReadOnlyCollection<MeshtasticNeighborEntry> Neighbors { get; init; } = [];

    public IReadOnlyCollection<MeshtasticTracerouteHop> TracerouteHops { get; init; } = [];
}
