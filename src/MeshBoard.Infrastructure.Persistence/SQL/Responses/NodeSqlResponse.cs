namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class NodeSqlResponse
{
    public required string NodeId { get; set; }

    public required string BrokerServer { get; set; }

    public string? ShortName { get; set; }

    public string? LongName { get; set; }

    public string? LastHeardAtUtc { get; set; }

    public string? LastHeardChannel { get; set; }

    public string? LastTextMessageAtUtc { get; set; }

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
