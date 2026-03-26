namespace MeshBoard.Client.Messages;

public sealed class ReceiveScopeTopic
{
    public string TopicPattern { get; init; } = string.Empty;

    public string SourceLabel { get; init; } = string.Empty;

    public bool IsFallback { get; init; }
}
