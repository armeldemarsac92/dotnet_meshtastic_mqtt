namespace MeshBoard.Contracts.Collector;

public sealed class CollectorChannelPacketHourlyRollup
{
    public DateTimeOffset BucketStartUtc { get; set; }

    public string ServerAddress { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string MeshVersion { get; set; } = string.Empty;

    public string ChannelName { get; set; } = string.Empty;

    public string PacketType { get; set; } = string.Empty;

    public int PacketCount { get; set; }

    public int ActiveNodeCount { get; set; }

    public DateTimeOffset FirstSeenAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }
}
