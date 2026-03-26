namespace MeshBoard.Contracts.Collector;

public sealed class CollectorTopologyQuery
{
    public string? ServerAddress { get; set; }

    public string? Region { get; set; }

    public string? ChannelName { get; set; }

    public int ActiveWithinHours { get; set; } = 24;

    public int MaxNodes { get; set; } = 10_000;

    public int MaxLinks { get; set; } = 20_000;

    public int TopCount { get; set; } = 10;
}
