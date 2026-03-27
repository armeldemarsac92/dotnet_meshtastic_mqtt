using System.Security.Cryptography;
using System.Text;

namespace MeshBoard.Contracts.CollectorEvents;

public static class CollectorEventPacketKey
{
    public static string Build(
        string brokerServer,
        string? fromNodeId,
        uint? packetId,
        string packetType,
        string? toNodeId,
        string? payloadPreview,
        DateTimeOffset receivedAtUtc)
    {
        if (packetId.HasValue && !string.IsNullOrWhiteSpace(fromNodeId))
        {
            return $"{brokerServer}|{fromNodeId}:{packetId.Value:x8}";
        }

        var rawKey = $"{brokerServer}|{packetType}|{fromNodeId}|{toNodeId}|{payloadPreview}|{receivedAtUtc:O}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
    }
}
