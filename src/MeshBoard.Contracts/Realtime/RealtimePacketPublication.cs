namespace MeshBoard.Contracts.Realtime;

public sealed class RealtimePacketPublication
{
    public string Topic { get; init; } = string.Empty;

    public byte[] Payload { get; init; } = [];

    public string ContentType { get; init; } = "application/json";
}
