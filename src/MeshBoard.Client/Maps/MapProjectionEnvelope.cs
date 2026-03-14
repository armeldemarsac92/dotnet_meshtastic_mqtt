namespace MeshBoard.Client.Maps;

public sealed record MapProjectionEnvelope
{
    public string NodeId { get; init; } = string.Empty;

    public string BrokerServer { get; init; } = string.Empty;

    public string? ShortName { get; init; }

    public string? LongName { get; init; }

    public string? Channel { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public int? BatteryLevelPercent { get; init; }

    public DateTimeOffset? LastHeardAtUtc { get; init; }

    public string? LastPacketType { get; init; }

    public string? LastPayloadPreview { get; init; }

    public int ObservedPacketCount { get; init; }

    public string DisplayName => LongName ?? ShortName ?? NodeId;
}
