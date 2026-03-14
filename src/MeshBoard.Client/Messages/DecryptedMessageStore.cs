using System.Security.Cryptography;
using System.Text;
using MeshBoard.Client.Realtime;

namespace MeshBoard.Client.Messages;

public sealed class DecryptedMessageStore
{
    private const int MaxRetainedMessages = 100;
    private readonly DecryptedMessageState _state;

    public DecryptedMessageStore(DecryptedMessageState state)
    {
        _state = state;
    }

    public event Action? Changed
    {
        add => _state.Changed += value;
        remove => _state.Changed -= value;
    }

    public DecryptedMessageSnapshot Current => _state.Snapshot;

    public void Clear()
    {
        _state.SetSnapshot(new());
    }

    public void Project(RealtimePacketWorkerResult packetResult)
    {
        ArgumentNullException.ThrowIfNull(packetResult);

        if (packetResult.RawPacket is null || packetResult.DecodedPacket is null)
        {
            return;
        }

        var rawPacket = packetResult.RawPacket;
        var decodedPacket = packetResult.DecodedPacket;

        if (string.IsNullOrWhiteSpace(rawPacket.SourceTopic))
        {
            throw new InvalidOperationException("A source topic is required to project a decoded message.");
        }

        var dedupeKey = BuildDedupeKey(rawPacket, decodedPacket);
        var receivedAtUtc = rawPacket.ReceivedAtUtc == default
            ? DateTimeOffset.UtcNow
            : rawPacket.ReceivedAtUtc;
        var nextMessage = new DecryptedMessageEnvelope
        {
            Id = dedupeKey,
            WorkspaceId = rawPacket.WorkspaceId?.Trim() ?? string.Empty,
            BrokerServer = rawPacket.BrokerServer?.Trim() ?? string.Empty,
            SourceTopic = rawPacket.SourceTopic.Trim(),
            DownstreamTopic = rawPacket.DownstreamTopic?.Trim() ?? string.Empty,
            ReceivedAtUtc = receivedAtUtc,
            IsEncrypted = rawPacket.IsEncrypted,
            DecryptResultClassification = rawPacket.DecryptResultClassification?.Trim() ?? RealtimePacketWorkerDecryptResultClassifications.NotAttempted,
            PacketId = rawPacket.PacketId,
            FromNodeNumber = rawPacket.FromNodeNumber,
            PortNumValue = decodedPacket.PortNumValue,
            PortNumName = decodedPacket.PortNumName?.Trim() ?? string.Empty,
            PacketType = decodedPacket.PacketType?.Trim() ?? string.Empty,
            PayloadPreview = decodedPacket.PayloadPreview?.Trim() ?? string.Empty,
            PayloadBase64 = decodedPacket.PayloadBase64?.Trim() ?? string.Empty,
            PayloadSizeBytes = decodedPacket.PayloadSizeBytes,
            SourceNodeNumber = decodedPacket.SourceNodeNumber,
            DestinationNodeNumber = decodedPacket.DestinationNodeNumber
        };

        var current = _state.Snapshot;
        if (current.Messages.Any(message => string.Equals(message.Id, dedupeKey, StringComparison.Ordinal)))
        {
            return;
        }

        var messages = current.Messages
            .Prepend(nextMessage)
            .Take(MaxRetainedMessages)
            .ToArray();

        _state.SetSnapshot(current with
        {
            Messages = messages,
            LastProjectedAtUtc = receivedAtUtc,
            TotalProjected = current.TotalProjected + 1
        });
    }

    private static string BuildDedupeKey(
        RealtimeRawPacketEvent rawPacket,
        RealtimeDecodedPacketEvent decodedPacket)
    {
        if (!string.IsNullOrWhiteSpace(rawPacket.BrokerServer) &&
            !string.IsNullOrWhiteSpace(rawPacket.SourceTopic) &&
            rawPacket.PacketId.HasValue &&
            rawPacket.FromNodeNumber.HasValue)
        {
            return $"{rawPacket.BrokerServer.Trim()}|{rawPacket.SourceTopic.Trim()}|{rawPacket.PacketId.Value}|{rawPacket.FromNodeNumber.Value}";
        }

        var stableInput = $"{rawPacket.BrokerServer?.Trim()}|{rawPacket.SourceTopic?.Trim()}|{decodedPacket.PortNumValue}|{decodedPacket.PayloadBase64?.Trim()}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(stableInput));
        return Convert.ToHexString(hashBytes);
    }
}
