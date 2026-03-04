namespace MeshBoard.Contracts.Messages;

public sealed class ComposeTextMessageResult
{
    public string Topic { get; set; } = string.Empty;

    public bool IsPrivate { get; set; }

    public string? ToNodeId { get; set; }

    public DateTimeOffset SentAtUtc { get; set; }

    public string StatusMessage { get; set; } = string.Empty;
}
