namespace MeshBoard.Contracts.Realtime;

public sealed class RealtimePacketEnvelope
{
    public string WorkspaceId { get; init; } = string.Empty;

    public string BrokerServer { get; init; } = string.Empty;

    public string Topic { get; init; } = string.Empty;

    public byte[] Payload { get; init; } = [];

    public DateTimeOffset ReceivedAtUtc { get; init; }
}
