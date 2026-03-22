namespace MeshBoard.Contracts.Collector;

public sealed class CollectorObservedPacketRollupRequest
{
    public long ChannelId { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string PacketType { get; set; } = string.Empty;

    public DateTimeOffset ObservedAtUtc { get; set; }
}
