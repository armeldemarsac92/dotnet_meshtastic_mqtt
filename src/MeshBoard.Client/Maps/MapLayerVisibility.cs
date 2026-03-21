using System.Text.Json.Serialization;

namespace MeshBoard.Client.Maps;

public sealed record MapLayerVisibility
{
    [JsonPropertyName("radioLinks")]
    public bool RadioLinks { get; init; }

    [JsonPropertyName("channelCohorts")]
    public bool ChannelCohorts { get; init; }
}
