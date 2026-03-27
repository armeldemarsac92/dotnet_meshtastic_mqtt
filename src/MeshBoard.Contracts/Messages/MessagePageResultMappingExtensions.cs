namespace MeshBoard.Contracts.Messages;

public static class MessagePageResultMappingExtensions
{
    public static MessagePageResult ToMessagePageResult(
        this IReadOnlyCollection<MessageSummary> items,
        int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        return new MessagePageResult
        {
            Items = items,
            TotalCount = totalCount
        };
    }
}
