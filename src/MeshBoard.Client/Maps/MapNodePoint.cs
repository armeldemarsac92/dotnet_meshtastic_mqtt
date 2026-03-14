using System.Text.Json.Serialization;

namespace MeshBoard.Client.Maps;

public sealed record MapNodePoint
{
    [JsonPropertyName("nodeId")]
    public string NodeId { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("channel")]
    public string? Channel { get; init; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; init; }

    [JsonPropertyName("batteryLevelPercent")]
    public int? BatteryLevelPercent { get; init; }

    public string BrokerServer { get; init; } = string.Empty;

    public DateTimeOffset? LastHeardAtUtc { get; init; }

    public string? LastPacketType { get; init; }

    public int ObservedPacketCount { get; init; }
}
