using MeshBoard.Client.Realtime;

namespace MeshBoard.Client.Messages;

public sealed record LiveMessageEnvelope
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string WorkspaceId { get; init; } = string.Empty;

    public string BrokerServer { get; init; } = string.Empty;

    public string DownstreamTopic { get; init; } = string.Empty;

    public string PayloadBase64 { get; init; } = string.Empty;

    public int PayloadSizeBytes { get; init; }

    public DateTimeOffset ReceivedAtUtc { get; init; }

    public string SourceTopic { get; init; } = string.Empty;

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
