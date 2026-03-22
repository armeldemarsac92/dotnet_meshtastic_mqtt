namespace MeshBoard.Client.Collector;

public sealed record CollectorMapNodeView
{
    public string NodeId { get; init; } = string.Empty;

    public string BrokerServer { get; init; } = string.Empty;

    public string? ShortName { get; init; }

    public string? LongName { get; init; }

    public string? ChannelKey { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public int? BatteryLevelPercent { get; init; }

    public DateTimeOffset? LastHeardAtUtc { get; init; }

    public string DisplayName => LongName ?? ShortName ?? NodeId;
}
