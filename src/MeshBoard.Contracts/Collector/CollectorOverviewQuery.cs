namespace MeshBoard.Contracts.Collector;

public sealed class CollectorOverviewQuery
{
    public string? ServerAddress { get; set; }

    public string? Region { get; set; }

    public string? ChannelName { get; set; }

    public int ActiveWithinHours { get; set; } = 24;

    public int LookbackHours { get; set; } = 24 * 7;

    public int MaxChannels { get; set; } = 20;

    public int TopPacketTypes { get; set; } = 3;
}
