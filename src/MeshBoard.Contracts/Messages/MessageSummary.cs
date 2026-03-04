namespace MeshBoard.Contracts.Messages;

public sealed class MessageSummary
{
    public Guid Id { get; set; }

    public string Topic { get; set; } = string.Empty;

    public string PacketType { get; set; } = string.Empty;

    public string FromNodeId { get; set; } = string.Empty;

    public string? ToNodeId { get; set; }

    public string PayloadPreview { get; set; } = string.Empty;

    public bool IsPrivate { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
