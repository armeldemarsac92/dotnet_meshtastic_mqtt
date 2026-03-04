namespace MeshBoard.Contracts.Topics;

public sealed class TopicCatalogEntry
{
    public string Label { get; set; } = string.Empty;

    public string TopicPattern { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public bool IsRecommended { get; set; }
}
