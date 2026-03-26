using MeshBoard.Client.Realtime;

namespace MeshBoard.Client.Channels;

public sealed class ChannelProjectionStore
{
    private const int MaxRetainedChannels = 250;
    private readonly ProjectionPacketDeduper _packetDeduper = new(4_096);
    private readonly ChannelProjectionState _state;

    public ChannelProjectionStore(ChannelProjectionState state)
    {
        _state = state;
    }

    public event Action? Changed
    {
        add => _state.Changed += value;
        remove => _state.Changed -= value;
    }

    public ChannelProjectionSnapshot Current => _state.Snapshot;

    public void Clear()
    {
        _packetDeduper.Clear();
        _state.SetSnapshot(new());
    }

    public void Project(RealtimePacketWorkerResult packetResult)
    {
        ArgumentNullException.ThrowIfNull(packetResult);

        if (packetResult.RawPacket is null || !TryParseChannel(packetResult.RawPacket.SourceTopic, out var channel))
        {
            return;
        }

        var rawPacket = packetResult.RawPacket;
        if (!_packetDeduper.TryTrack(rawPacket, packetResult.DecodedPacket))
        {
            return;
        }

        var observedAtUtc = rawPacket.ReceivedAtUtc == default
            ? DateTimeOffset.UtcNow
            : rawPacket.ReceivedAtUtc;
        var packetType = packetResult.DecodedPacket?.PacketType?.Trim() ?? "Observed Packet";
        var payloadPreview = packetResult.DecodedPacket?.PayloadPreview?.Trim() ?? string.Empty;
        var observedNodeIds = ResolveObservedNodeIds(packetResult).ToArray();
        var current = _state.Snapshot;
        var channelId = BuildId(rawPacket.BrokerServer, channel.ChannelKey);
        var existing = current.Channels.FirstOrDefault(
            entry => string.Equals(entry.Id, channelId, StringComparison.Ordinal));
        var next = existing is null
            ? CreateChannel(channelId, rawPacket.BrokerServer, channel, rawPacket.SourceTopic, observedAtUtc, packetType, payloadPreview, observedNodeIds)
            : Merge(existing, rawPacket.BrokerServer, rawPacket.SourceTopic, observedAtUtc, packetType, payloadPreview, observedNodeIds);
        var channels = current.Channels
            .Where(entry => !string.Equals(entry.Id, channelId, StringComparison.Ordinal))
            .Append(next)
            .OrderByDescending(entry => entry.LastObservedAtUtc)
            .ThenBy(entry => entry.ChannelKey, StringComparer.OrdinalIgnoreCase)
            .Take(MaxRetainedChannels)
            .ToArray();

        _state.SetSnapshot(current with
        {
            Channels = channels,
            LastProjectedAtUtc = observedAtUtc,
            TotalProjected = current.TotalProjected + 1
        });
    }

    private static ChannelProjectionEnvelope CreateChannel(
        string channelId,
        string brokerServer,
        ObservedChannel channel,
        string sourceTopic,
        DateTimeOffset observedAtUtc,
        string packetType,
        string payloadPreview,
        IReadOnlyList<string> observedNodeIds)
    {
        return new ChannelProjectionEnvelope
        {
            Id = channelId,
            BrokerServer = NormalizeText(brokerServer),
            Region = channel.Region,
            ChannelName = channel.ChannelName,
            ChannelKey = channel.ChannelKey,
            LatestSourceTopic = NormalizeText(sourceTopic),
            LastObservedAtUtc = observedAtUtc,
            LastPacketType = packetType,
            LastPayloadPreview = payloadPreview,
            ObservedPacketCount = 1,
            ObservedNodeIds = observedNodeIds
        };
    }

    private static ChannelProjectionEnvelope Merge(
        ChannelProjectionEnvelope existing,
        string brokerServer,
        string sourceTopic,
        DateTimeOffset observedAtUtc,
        string packetType,
        string payloadPreview,
        IReadOnlyList<string> observedNodeIds)
    {
        var mergedNodeIds = existing.ObservedNodeIds
            .Concat(observedNodeIds)
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(nodeId => nodeId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var isLatest = !existing.LastObservedAtUtc.HasValue || observedAtUtc >= existing.LastObservedAtUtc.Value;

        return existing with
        {
            BrokerServer = string.IsNullOrWhiteSpace(brokerServer) ? existing.BrokerServer : brokerServer.Trim(),
            LatestSourceTopic = isLatest ? NormalizeText(sourceTopic) : existing.LatestSourceTopic,
            LastObservedAtUtc = isLatest ? observedAtUtc : existing.LastObservedAtUtc,
            LastPacketType = isLatest && !string.IsNullOrWhiteSpace(packetType) ? packetType : existing.LastPacketType,
            LastPayloadPreview = isLatest && !string.IsNullOrWhiteSpace(payloadPreview) ? payloadPreview : existing.LastPayloadPreview,
            ObservedPacketCount = existing.ObservedPacketCount + 1,
            ObservedNodeIds = mergedNodeIds
        };
    }

    private static IEnumerable<string> ResolveObservedNodeIds(RealtimePacketWorkerResult packetResult)
    {
        var projectionNodeId = packetResult.DecodedPacket?.NodeProjection?.NodeId;
        if (!string.IsNullOrWhiteSpace(projectionNodeId))
        {
            yield return projectionNodeId.Trim();
            yield break;
        }

        var decodedNodeNumber = packetResult.DecodedPacket?.SourceNodeNumber;
        if (decodedNodeNumber.HasValue)
        {
            var decodedNodeId = FormatNodeId(decodedNodeNumber.Value);
            if (!string.IsNullOrWhiteSpace(decodedNodeId))
            {
                yield return decodedNodeId;
                yield break;
            }
        }

        if (packetResult.RawPacket?.FromNodeNumber is { } rawNodeNumber)
        {
            var rawNodeId = FormatNodeId(rawNodeNumber);
            if (!string.IsNullOrWhiteSpace(rawNodeId))
            {
                yield return rawNodeId;
            }
        }
    }

    private static bool TryParseChannel(string? sourceTopic, out ObservedChannel channel)
    {
        channel = default;

        if (string.IsNullOrWhiteSpace(sourceTopic))
        {
            return false;
        }

        var segments = sourceTopic
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 5 ||
            !string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var topicType = segments[3];
        if (!string.Equals(topicType, "e", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(topicType, "json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var region = NormalizeText(segments[1]);
        var channelName = NormalizeText(segments[4]);

        if (string.IsNullOrWhiteSpace(region) ||
            string.IsNullOrWhiteSpace(channelName) ||
            channelName is "#" or "+")
        {
            return false;
        }

        channel = new ObservedChannel(region, channelName, $"{region}/{channelName}");
        return true;
    }

    private static string BuildId(string? brokerServer, string channelKey)
    {
        return $"{NormalizeText(brokerServer)}|{channelKey}";
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string FormatNodeId(uint nodeNumber)
    {
        return nodeNumber == 0 ? string.Empty : $"!{nodeNumber:x8}";
    }

    private readonly record struct ObservedChannel(string Region, string ChannelName, string ChannelKey);
}
