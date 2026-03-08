namespace MeshBoard.Contracts.Messages;

public sealed class MessagePageResult
{
    public IReadOnlyCollection<MessageSummary> Items { get; set; } = [];

    public int TotalCount { get; set; }
}
