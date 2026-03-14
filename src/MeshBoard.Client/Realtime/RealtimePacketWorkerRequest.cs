namespace MeshBoard.Client.Realtime;

public sealed class RealtimePacketWorkerRequest
{
    public string DownstreamTopic { get; init; } = string.Empty;

    public string PayloadBase64 { get; init; } = string.Empty;

    public DateTimeOffset ReceivedAtUtc { get; init; }
}
