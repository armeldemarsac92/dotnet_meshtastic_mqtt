namespace MeshBoard.Contracts.Meshtastic;

public sealed class MeshtasticEnvelope
{
    public string WorkspaceId { get; set; } = string.Empty;

    public string BrokerServer { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public string PacketType { get; set; } = string.Empty;

    public uint? PacketId { get; set; }

    public string PayloadPreview { get; set; } = string.Empty;

    public string? FromNodeId { get; set; }

    public string? ToNodeId { get; set; }

    public bool IsPrivate { get; set; }

    public string? ShortName { get; set; }

    public string? LongName { get; set; }

    public string? LastHeardChannel { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int? BatteryLevelPercent { get; set; }

    public double? Voltage { get; set; }

    public double? ChannelUtilization { get; set; }

    public double? AirUtilTx { get; set; }

    public long? UptimeSeconds { get; set; }

    public double? TemperatureCelsius { get; set; }

    public double? RelativeHumidity { get; set; }

    public double? BarometricPressure { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
