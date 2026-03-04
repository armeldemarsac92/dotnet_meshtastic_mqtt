namespace MeshBoard.Contracts.Messages;

public sealed class ComposeTextMessageRequest
{
    public string Text { get; set; } = string.Empty;

    public string? ToNodeId { get; set; }
}
