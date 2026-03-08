namespace MeshBoard.Contracts.Configuration;

public sealed class MapOptions
{
    public string Provider { get; set; } = "Mapbox";

    public string FallbackProvider { get; set; } = "OpenStreetMap";

    public string MapboxAccessToken { get; set; } = string.Empty;

    public string MapboxStyleId { get; set; } = "satellite-streets-v12";

    public string MapboxUsername { get; set; } = "mapbox";
}
