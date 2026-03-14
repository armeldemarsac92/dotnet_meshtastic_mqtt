using System.Text.Json.Serialization;

namespace MeshBoard.Client.Maps;

public sealed record MapNodeActivity
{
    [JsonPropertyName("nodeId")]
    public string NodeId { get; init; } = string.Empty;

    [JsonPropertyName("pulseCount")]
    public int PulseCount { get; init; }
}
