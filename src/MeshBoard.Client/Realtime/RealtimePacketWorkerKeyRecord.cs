namespace MeshBoard.Client.Realtime;

public sealed class RealtimePacketWorkerKeyRecord
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string TopicPattern { get; init; } = string.Empty;

    public string? BrokerServerProfileId { get; init; }

    public string NormalizedKeyBase64 { get; init; } = string.Empty;

    public int KeyLengthBytes { get; init; }
}
