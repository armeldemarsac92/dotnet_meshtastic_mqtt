namespace MeshBoard.Client.Realtime;

public sealed class RealtimeNodeProjectionEvent
{
    public string NodeId { get; init; } = string.Empty;

    public uint? NodeNumber { get; init; }

    public string? LastHeardChannel { get; init; }

    public DateTimeOffset LastHeardAtUtc { get; init; }

    public DateTimeOffset? LastTextMessageAtUtc { get; init; }

    public string? ShortName { get; init; }

    public string? LongName { get; init; }

    public string? PacketType { get; init; }

    public string? PayloadPreview { get; init; }

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
}
