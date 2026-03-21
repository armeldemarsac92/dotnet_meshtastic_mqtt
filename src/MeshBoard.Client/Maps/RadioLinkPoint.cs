using System.Text.Json.Serialization;

namespace MeshBoard.Client.Maps;

public sealed record RadioLinkPoint
{
    [JsonPropertyName("sourceNodeId")]
    public string SourceNodeId { get; init; } = string.Empty;

    [JsonPropertyName("targetNodeId")]
    public string TargetNodeId { get; init; } = string.Empty;

    [JsonPropertyName("snrDb")]
    public float? SnrDb { get; init; }

    [JsonPropertyName("sourceLatitude")]
    public double SourceLatitude { get; init; }

    [JsonPropertyName("sourceLongitude")]
    public double SourceLongitude { get; init; }

    [JsonPropertyName("targetLatitude")]
    public double TargetLatitude { get; init; }

    [JsonPropertyName("targetLongitude")]
    public double TargetLongitude { get; init; }
}
