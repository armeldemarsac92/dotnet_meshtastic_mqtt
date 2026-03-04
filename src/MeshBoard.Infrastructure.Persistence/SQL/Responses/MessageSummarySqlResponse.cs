namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class MessageSummarySqlResponse
{
    public required string Id { get; set; }

    public required string Topic { get; set; }

    public required string PacketType { get; set; }

    public required string FromNodeId { get; set; }

    public string? FromNodeShortName { get; set; }

    public string? FromNodeLongName { get; set; }

    public string? ToNodeId { get; set; }

    public required string PayloadPreview { get; set; }

    public int IsPrivate { get; set; }

    public required string ReceivedAtUtc { get; set; }
}
