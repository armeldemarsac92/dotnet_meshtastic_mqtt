namespace MeshBoard.Contracts.Topics;

public static class TopicCatalogEntryMappingExtensions
{
    public static TopicCatalogEntry ToTopicCatalogEntry(
        this (string Region, string Channel, string TopicPattern, bool IsRecommended) source)
    {
        return new TopicCatalogEntry
        {
            Label = $"{source.Region} · {source.Channel}",
            TopicPattern = source.TopicPattern,
            Region = source.Region,
            Channel = source.Channel,
            IsRecommended = source.IsRecommended
        };
    }
}
