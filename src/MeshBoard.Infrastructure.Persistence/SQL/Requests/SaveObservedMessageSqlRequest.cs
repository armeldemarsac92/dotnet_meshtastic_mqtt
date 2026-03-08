namespace MeshBoard.Infrastructure.Persistence.SQL.Requests;

internal sealed class SaveObservedMessageSqlRequest
{
    public required string Id { get; set; }

    public required string WorkspaceId { get; set; }

    public required string BrokerServer { get; set; }

    public required string Topic { get; set; }

    public required string PacketType { get; set; }

    public required string MessageKey { get; set; }

    public required string FromNodeId { get; set; }

    public string? ToNodeId { get; set; }

    public required string PayloadPreview { get; set; }

    public int IsPrivate { get; set; }

    public required string ReceivedAtUtc { get; set; }
}
