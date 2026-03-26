namespace MeshBoard.Contracts.Collector;

public sealed class CollectorNodePacketHourlyRollup
{
    public DateTimeOffset BucketStartUtc { get; set; }

    public string ServerAddress { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string MeshVersion { get; set; } = string.Empty;

    public string ChannelName { get; set; } = string.Empty;

    public string NodeId { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    public string? LongName { get; set; }

    public string PacketType { get; set; } = string.Empty;

    public int PacketCount { get; set; }

    public DateTimeOffset FirstSeenAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }
}
