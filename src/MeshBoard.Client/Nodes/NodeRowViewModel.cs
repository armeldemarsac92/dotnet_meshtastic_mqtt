using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Client.Nodes;

public sealed record NodeRowViewModel
{
    public string NodeId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? LongName { get; init; }
    public string BrokerServer { get; init; } = string.Empty;
    public string? LastHeardChannel { get; init; }
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
    public string? LastPacketType { get; init; }
    public DateTimeOffset? LastHeardAtUtc { get; init; }

    public static NodeRowViewModel From(NodeProjectionEnvelope e) => new()
    {
        NodeId = e.NodeId,
        DisplayName = e.DisplayName,
        ShortName = e.ShortName,
        LongName = e.LongName,
        BrokerServer = e.BrokerServer,
        LastHeardChannel = e.LastHeardChannel,
        LastKnownLatitude = e.LastKnownLatitude,
        LastKnownLongitude = e.LastKnownLongitude,
        BatteryLevelPercent = e.BatteryLevelPercent,
        Voltage = e.Voltage,
        ChannelUtilization = e.ChannelUtilization,
        AirUtilTx = e.AirUtilTx,
        UptimeSeconds = e.UptimeSeconds,
        TemperatureCelsius = e.TemperatureCelsius,
        RelativeHumidity = e.RelativeHumidity,
        BarometricPressure = e.BarometricPressure,
        ObservedPacketCount = e.ObservedPacketCount,
        LastPacketType = e.LastPacketType,
        LastHeardAtUtc = e.LastHeardAtUtc
    };

    public static NodeRowViewModel From(NodeSummary s) => new()
    {
        NodeId = s.NodeId,
        DisplayName = s.LongName ?? s.ShortName ?? s.NodeId,
        ShortName = s.ShortName,
        LongName = s.LongName,
        BrokerServer = s.BrokerServer,
        LastHeardChannel = s.LastHeardChannel,
        LastKnownLatitude = s.LastKnownLatitude,
        LastKnownLongitude = s.LastKnownLongitude,
        BatteryLevelPercent = s.BatteryLevelPercent,
        Voltage = s.Voltage,
        ChannelUtilization = s.ChannelUtilization,
        AirUtilTx = s.AirUtilTx,
        UptimeSeconds = s.UptimeSeconds,
        TemperatureCelsius = s.TemperatureCelsius,
        RelativeHumidity = s.RelativeHumidity,
        BarometricPressure = s.BarometricPressure,
        ObservedPacketCount = 0,
        LastPacketType = null,
        LastHeardAtUtc = s.LastHeardAtUtc
    };
}
