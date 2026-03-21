namespace MeshBoard.Contracts.Collector;

public sealed class CollectorPacketStatsQuery
{
    public string? ServerAddress { get; set; }

    public string? Region { get; set; }

    public string? ChannelName { get; set; }

    public string? NodeId { get; set; }

    public string? PacketType { get; set; }

    public int LookbackHours { get; set; } = 24 * 7;

    public int MaxRows { get; set; } = 500;
}
