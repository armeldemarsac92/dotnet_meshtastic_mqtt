namespace MeshBoard.Contracts.Nodes;

public sealed class NodeSummary
{
    public string NodeId { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    public string? LongName { get; set; }

    public DateTimeOffset? LastHeardAtUtc { get; set; }

    public string? LastHeardChannel { get; set; }

    public DateTimeOffset? LastTextMessageAtUtc { get; set; }

    public double? LastKnownLatitude { get; set; }

    public double? LastKnownLongitude { get; set; }

    public int? BatteryLevelPercent { get; set; }

    public double? Voltage { get; set; }

    public double? ChannelUtilization { get; set; }

    public double? AirUtilTx { get; set; }

    public long? UptimeSeconds { get; set; }

    public double? TemperatureCelsius { get; set; }

    public double? RelativeHumidity { get; set; }

    public double? BarometricPressure { get; set; }
}
