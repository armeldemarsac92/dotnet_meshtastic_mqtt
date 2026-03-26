namespace MeshBoard.Client.Realtime;

public sealed class RealtimePacketWorkerResult
{
    public bool IsSuccess { get; init; }

    public string DecryptResultClassification { get; init; } = string.Empty;

    public string? FailureClassification { get; init; }

    public string? ErrorDetail { get; init; }

    public RealtimeRawPacketEvent? RawPacket { get; init; }

    public RealtimeDecodedPacketEvent? DecodedPacket { get; init; }
}
