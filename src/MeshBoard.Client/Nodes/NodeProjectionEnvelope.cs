namespace MeshBoard.Client.Nodes;

public sealed record NodeProjectionEnvelope
{
    public string NodeId { get; init; } = string.Empty;

    public string BrokerServer { get; init; } = string.Empty;

    public string? ShortName { get; init; }

    public string? LongName { get; init; }

    public DateTimeOffset? LastHeardAtUtc { get; init; }

    public string? LastHeardChannel { get; init; }

    public DateTimeOffset? LastTextMessageAtUtc { get; init; }

    public string? LastPacketType { get; init; }

    public string? LastPayloadPreview { get; init; }

    public double? LastKnownLatitude { get; init; }

    public double? LastKnownLongitude { get; init; }

    public int? BatteryLevelPercent { get; init; }

    public double? Voltage { get; init; }

    public double? ChannelUtilization { get; init; }

    public double? AirUtilTx { get; init; }

    public long? UptimeSeconds { get; init; }

    public double? TemperatureCelsius { get; init; }

    public double? RelativeHumidity { get; init; }

    public double? BarometricPressure { get; init; }

    public int ObservedPacketCount { get; init; }

    public bool HasLocation => LastKnownLatitude.HasValue && LastKnownLongitude.HasValue;

    public bool HasTelemetry =>
        BatteryLevelPercent.HasValue ||
        Voltage.HasValue ||
        ChannelUtilization.HasValue ||
        AirUtilTx.HasValue ||
        UptimeSeconds.HasValue ||
        TemperatureCelsius.HasValue ||
        RelativeHumidity.HasValue ||
        BarometricPressure.HasValue;

    public string DisplayName => LongName ?? ShortName ?? NodeId;
}
