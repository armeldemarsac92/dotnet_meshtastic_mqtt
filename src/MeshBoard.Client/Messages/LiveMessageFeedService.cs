using MeshBoard.Client.Realtime;

namespace MeshBoard.Client.Messages;

public sealed class LiveMessageFeedService
{
    private const int MaxRetainedMessages = 100;
    private readonly LiveMessageFeedState _state;

    public LiveMessageFeedService(LiveMessageFeedState state)
    {
        _state = state;
    }

    public event Action? Changed
    {
        add => _state.Changed += value;
        remove => _state.Changed -= value;
    }

    public LiveMessageFeedSnapshot Current => _state.Snapshot;

    public void Clear()
    {
        _state.SetSnapshot(new());
    }

    public void RecordMessage(RealtimeRawPacketEvent rawPacket)
    {
        ArgumentNullException.ThrowIfNull(rawPacket);

        if (string.IsNullOrWhiteSpace(rawPacket.SourceTopic))
        {
            throw new InvalidOperationException("A source topic is required.");
        }

        var current = _state.Snapshot;
        var receivedAtUtc = rawPacket.ReceivedAtUtc == default
            ? DateTimeOffset.UtcNow
            : rawPacket.ReceivedAtUtc;
        var nextMessage = new LiveMessageEnvelope
        {
            WorkspaceId = rawPacket.WorkspaceId?.Trim() ?? string.Empty,
            BrokerServer = rawPacket.BrokerServer?.Trim() ?? string.Empty,
            DownstreamTopic = rawPacket.DownstreamTopic?.Trim() ?? string.Empty,
            PayloadBase64 = rawPacket.PayloadBase64?.Trim() ?? string.Empty,
            PayloadSizeBytes = rawPacket.PayloadSizeBytes,
            ReceivedAtUtc = receivedAtUtc,
            SourceTopic = rawPacket.SourceTopic.Trim(),
            IsEncrypted = rawPacket.IsEncrypted,
            DecryptionAttempted = rawPacket.DecryptionAttempted,
            DecryptionSucceeded = rawPacket.DecryptionSucceeded,
            DecryptResultClassification = rawPacket.DecryptResultClassification?.Trim() ?? RealtimePacketWorkerDecryptResultClassifications.NotAttempted,
            FailureClassification = string.IsNullOrWhiteSpace(rawPacket.FailureClassification)
                ? null
                : rawPacket.FailureClassification.Trim(),
            DecryptedPayloadBase64 = string.IsNullOrWhiteSpace(rawPacket.DecryptedPayloadBase64)
                ? null
                : rawPacket.DecryptedPayloadBase64.Trim(),
            MatchedKeyId = string.IsNullOrWhiteSpace(rawPacket.MatchedKeyId)
                ? null
                : rawPacket.MatchedKeyId.Trim(),
            FromNodeNumber = rawPacket.FromNodeNumber,
            PacketId = rawPacket.PacketId
        };

        var messages = current.Messages
            .Prepend(nextMessage)
            .Take(MaxRetainedMessages)
            .ToArray();

        _state.SetSnapshot(current with
        {
            Messages = messages,
            LastReceivedAtUtc = receivedAtUtc,
            TotalReceived = current.TotalReceived + 1
        });
    }
}
