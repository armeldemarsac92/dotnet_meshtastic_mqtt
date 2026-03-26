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

    public void RecordMessage(
        RealtimeRawPacketEvent rawPacket,
        RealtimeDecodedPacketEvent? decodedPacket = null)
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
            PacketId = rawPacket.PacketId,
            RxSnr = rawPacket.RxSnr,
            RxRssi = rawPacket.RxRssi,
            HopLimit = rawPacket.HopLimit,
            HopStart = rawPacket.HopStart,
            GatewayNodeId = string.IsNullOrWhiteSpace(rawPacket.GatewayNodeId) ? null : rawPacket.GatewayNodeId.Trim(),
            PortNumValue = decodedPacket?.PortNumValue,
            PortNumName = string.IsNullOrWhiteSpace(decodedPacket?.PortNumName)
                ? null
                : decodedPacket.PortNumName.Trim(),
            PacketType = string.IsNullOrWhiteSpace(decodedPacket?.PacketType)
                ? null
                : decodedPacket.PacketType.Trim(),
            PayloadPreview = string.IsNullOrWhiteSpace(decodedPacket?.PayloadPreview)
                ? null
                : decodedPacket.PayloadPreview.Trim(),
            DecodedPayloadBase64 = string.IsNullOrWhiteSpace(decodedPacket?.PayloadBase64)
                ? null
                : decodedPacket.PayloadBase64.Trim(),
            DecodedPayloadSizeBytes = decodedPacket?.PayloadSizeBytes,
            DecodedSourceNodeNumber = decodedPacket?.SourceNodeNumber,
            DecodedDestinationNodeNumber = decodedPacket?.DestinationNodeNumber,
            TracerouteInfo = decodedPacket?.TracerouteInfo
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
