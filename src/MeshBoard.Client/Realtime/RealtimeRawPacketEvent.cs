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

    public bool IsEncrypted { get; init; }

    public bool DecryptionAttempted { get; init; }

    public bool DecryptionSucceeded { get; init; }

    public string DecryptResultClassification { get; init; } = RealtimePacketWorkerDecryptResultClassifications.NotAttempted;

    public string? FailureClassification { get; init; }

    public string? DecryptedPayloadBase64 { get; init; }

    public string? MatchedKeyId { get; init; }

    public uint? FromNodeNumber { get; init; }

    public uint? PacketId { get; init; }
}
