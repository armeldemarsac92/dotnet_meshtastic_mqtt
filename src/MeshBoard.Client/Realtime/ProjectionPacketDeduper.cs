using System.Security.Cryptography;
using System.Text;

namespace MeshBoard.Client.Realtime;

internal sealed class ProjectionPacketDeduper
{
    private readonly Queue<string> _dedupeOrder = new();
    private readonly HashSet<string> _dedupeSet = new(StringComparer.Ordinal);
    private readonly int _maxTrackedPackets;

    public ProjectionPacketDeduper(int maxTrackedPackets)
    {
        if (maxTrackedPackets <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTrackedPackets));
        }

        _maxTrackedPackets = maxTrackedPackets;
    }

    public void Clear()
    {
        _dedupeOrder.Clear();
        _dedupeSet.Clear();
    }

    public bool TryTrack(RealtimeRawPacketEvent rawPacket, RealtimeDecodedPacketEvent? decodedPacket)
    {
        ArgumentNullException.ThrowIfNull(rawPacket);

        var dedupeKey = BuildDedupeKey(rawPacket, decodedPacket);
        if (!_dedupeSet.Add(dedupeKey))
        {
            return false;
        }

        _dedupeOrder.Enqueue(dedupeKey);
        while (_dedupeOrder.Count > _maxTrackedPackets)
        {
            _dedupeSet.Remove(_dedupeOrder.Dequeue());
        }

        return true;
    }

    private static string BuildDedupeKey(
        RealtimeRawPacketEvent rawPacket,
        RealtimeDecodedPacketEvent? decodedPacket)
    {
        if (!string.IsNullOrWhiteSpace(rawPacket.BrokerServer) &&
            !string.IsNullOrWhiteSpace(rawPacket.SourceTopic) &&
            rawPacket.PacketId.HasValue &&
            rawPacket.FromNodeNumber.HasValue)
        {
            return $"{rawPacket.BrokerServer.Trim()}|{rawPacket.SourceTopic.Trim()}|{rawPacket.PacketId.Value}|{rawPacket.FromNodeNumber.Value}";
        }

        var stablePayload = decodedPacket?.PayloadBase64?.Trim();
        if (string.IsNullOrWhiteSpace(stablePayload))
        {
            stablePayload = rawPacket.DecryptedPayloadBase64?.Trim();
        }

        if (string.IsNullOrWhiteSpace(stablePayload))
        {
            stablePayload = rawPacket.PayloadBase64?.Trim() ?? string.Empty;
        }

        var stableInput =
            $"{rawPacket.BrokerServer?.Trim()}|{rawPacket.SourceTopic?.Trim()}|{decodedPacket?.PortNumValue}|{stablePayload}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(stableInput));
        return Convert.ToHexString(hashBytes);
    }
}
