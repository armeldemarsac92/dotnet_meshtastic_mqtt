namespace MeshBoard.Client.Realtime;

public sealed class RealtimeRawPacketEvent
{
    public string WorkspaceId { get; init; } = string.Empty;

    public string BrokerServer { get; init; } = string.Empty;

    public string SourceTopic { get; init; } = string.Empty;

    public string DownstreamTopic { get; init; } = string.Empty;

    public string PayloadBase64 { get; init; } = string.Empty;

    public int PayloadSizeBytes { get; init; }

    public DateTimeOffset ReceivedAtUtc { get; init; }
}
