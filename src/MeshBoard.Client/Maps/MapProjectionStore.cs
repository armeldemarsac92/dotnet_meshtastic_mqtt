using MeshBoard.Client.Realtime;

namespace MeshBoard.Client.Maps;

public sealed class MapProjectionStore
{
    private const int MaxRetainedNodes = 5_000;
    private readonly Dictionary<string, int> _pendingActivityPulseCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ProjectionPacketDeduper _packetDeduper = new(4_096);
    private readonly MapProjectionState _state;

    public MapProjectionStore(MapProjectionState state)
    {
        _state = state;
    }

    public event Action? Changed
    {
        add => _state.Changed += value;
        remove => _state.Changed -= value;
    }

    public MapProjectionSnapshot Current => _state.Snapshot;

    public void Clear()
    {
        _packetDeduper.Clear();
        _pendingActivityPulseCounts.Clear();
        _state.SetSnapshot(new());
    }

    public void Project(RealtimePacketWorkerResult packetResult)
    {
        ArgumentNullException.ThrowIfNull(packetResult);

        if (packetResult.RawPacket is null || packetResult.DecodedPacket?.NodeProjection is null)
        {
            return;
        }

        var rawPacket = packetResult.RawPacket;
        var decodedPacket = packetResult.DecodedPacket;
        var projection = decodedPacket.NodeProjection;

        if (string.IsNullOrWhiteSpace(projection.NodeId))
        {
            return;
        }

        if (!_packetDeduper.TryTrack(rawPacket, packetResult.DecodedPacket))
        {
            return;
        }

        var nodeId = NormalizeRequiredText(projection.NodeId);
        var current = _state.Snapshot;
        var existing = current.Nodes.FirstOrDefault(
            node => string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        var observedAtUtc = projection.LastHeardAtUtc == default
            ? rawPacket.ReceivedAtUtc == default
                ? DateTimeOffset.UtcNow
                : rawPacket.ReceivedAtUtc
            : projection.LastHeardAtUtc;
        var isLatest = existing is null ||
            !existing.LastHeardAtUtc.HasValue ||
            observedAtUtc >= existing.LastHeardAtUtc.Value;
        var latitude = MergeNullableValue(isLatest, projection.LastKnownLatitude, existing?.Latitude);
        var longitude = MergeNullableValue(isLatest, projection.LastKnownLongitude, existing?.Longitude);

        if (!latitude.HasValue || !longitude.HasValue)
        {
            return;
        }

        var nextNode = existing is null
            ? CreateNode(projection, decodedPacket, rawPacket, latitude.Value, longitude.Value, observedAtUtc)
            : Merge(existing, projection, decodedPacket, rawPacket, latitude.Value, longitude.Value, observedAtUtc, isLatest);

        var nodes = current.Nodes
            .Where(node => !string.Equals(node.NodeId, nextNode.NodeId, StringComparison.OrdinalIgnoreCase))
            .Append(nextNode)
            .OrderByDescending(node => node.LastHeardAtUtc)
            .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxRetainedNodes)
            .ToArray();

        RecordActivityPulse(nextNode.NodeId);

        _state.SetSnapshot(current with
        {
            Nodes = nodes,
            LastProjectedAtUtc = observedAtUtc,
            TotalProjected = current.TotalProjected + 1
        });
    }

    public IReadOnlyList<MapNodeActivity> DrainActivityPulses()
    {
        if (_pendingActivityPulseCounts.Count == 0)
        {
            return [];
        }

        var pulses = _pendingActivityPulseCounts
            .Select(entry => new MapNodeActivity
            {
                NodeId = entry.Key,
                PulseCount = entry.Value
            })
            .ToArray();

        _pendingActivityPulseCounts.Clear();
        return pulses;
    }

    public static IReadOnlyList<MapProjectionEnvelope> ApplyQuery(
        MapProjectionSnapshot snapshot,
        string? searchText,
        string? focusedChannel)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var normalizedSearchText = searchText?.Trim() ?? string.Empty;

        return snapshot.Nodes
            .Where(node => MatchesSearchText(node, normalizedSearchText))
            .Where(node => MatchesFocusedChannel(node, focusedChannel))
            .OrderByDescending(node => node.LastHeardAtUtc)
            .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<MapNodePoint> ToMapNodePoints(IReadOnlyCollection<MapProjectionEnvelope> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        return nodes.Select(
                node => new MapNodePoint
                {
                    NodeId = node.NodeId,
                    DisplayName = node.DisplayName,
                    Channel = node.Channel,
                    Latitude = node.Latitude,
                    Longitude = node.Longitude,
                    BatteryLevelPercent = node.BatteryLevelPercent
                })
            .ToArray();
    }

    private void RecordActivityPulse(string nodeId)
    {
        _pendingActivityPulseCounts.TryGetValue(nodeId, out var pulseCount);
        _pendingActivityPulseCounts[nodeId] = pulseCount + 1;
    }

    private static MapProjectionEnvelope CreateNode(
        RealtimeNodeProjectionEvent projection,
        RealtimeDecodedPacketEvent decodedPacket,
        RealtimeRawPacketEvent rawPacket,
        double latitude,
        double longitude,
        DateTimeOffset observedAtUtc)
    {
        return new MapProjectionEnvelope
        {
            NodeId = NormalizeRequiredText(projection.NodeId),
            BrokerServer = rawPacket.BrokerServer?.Trim() ?? string.Empty,
            ShortName = NormalizeNullableText(projection.ShortName),
            LongName = NormalizeNullableText(projection.LongName),
            Channel = NormalizeNullableText(projection.LastHeardChannel),
            Latitude = latitude,
            Longitude = longitude,
            BatteryLevelPercent = projection.BatteryLevelPercent,
            LastHeardAtUtc = observedAtUtc,
            LastPacketType = NormalizeNullableText(decodedPacket.PacketType),
            LastPayloadPreview = NormalizeNullableText(decodedPacket.PayloadPreview),
            ObservedPacketCount = 1
        };
    }

    private static MapProjectionEnvelope Merge(
        MapProjectionEnvelope existing,
        RealtimeNodeProjectionEvent projection,
        RealtimeDecodedPacketEvent decodedPacket,
        RealtimeRawPacketEvent rawPacket,
        double latitude,
        double longitude,
        DateTimeOffset observedAtUtc,
        bool isLatest)
    {
        return existing with
        {
            BrokerServer = MergeRequiredText(isLatest, rawPacket.BrokerServer, existing.BrokerServer),
            ShortName = MergeNullableText(isLatest, projection.ShortName, existing.ShortName),
            LongName = MergeNullableText(isLatest, projection.LongName, existing.LongName),
            Channel = MergeNullableText(isLatest, projection.LastHeardChannel, existing.Channel),
            Latitude = latitude,
            Longitude = longitude,
            BatteryLevelPercent = MergeNullableValue(isLatest, projection.BatteryLevelPercent, existing.BatteryLevelPercent),
            LastHeardAtUtc = Max(existing.LastHeardAtUtc, observedAtUtc),
            LastPacketType = MergeNullableText(isLatest, decodedPacket.PacketType, existing.LastPacketType),
            LastPayloadPreview = MergeNullableText(isLatest, decodedPacket.PayloadPreview, existing.LastPayloadPreview),
            ObservedPacketCount = existing.ObservedPacketCount + 1
        };
    }

    private static bool MatchesSearchText(MapProjectionEnvelope node, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return Contains(node.NodeId, searchText) ||
            Contains(node.ShortName, searchText) ||
            Contains(node.LongName, searchText) ||
            Contains(node.Channel, searchText) ||
            Contains(node.LastPacketType, searchText);
    }

    private static bool MatchesFocusedChannel(MapProjectionEnvelope node, string? focusedChannel)
    {
        return string.IsNullOrWhiteSpace(focusedChannel) ||
            string.Equals(node.Channel, focusedChannel, StringComparison.Ordinal);
    }

    private static bool Contains(string? source, string filter)
    {
        return !string.IsNullOrWhiteSpace(source) &&
            source.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? Max(DateTimeOffset? left, DateTimeOffset? right)
    {
        return left.HasValue && right.HasValue
            ? left.Value >= right.Value ? left : right
            : left ?? right;
    }

    private static string NormalizeRequiredText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? NormalizeNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? MergeNullableText(bool isLatest, string? candidate, string? fallback)
    {
        var normalizedCandidate = NormalizeNullableText(candidate);
        var normalizedFallback = NormalizeNullableText(fallback);

        return isLatest
            ? normalizedCandidate ?? normalizedFallback
            : normalizedFallback ?? normalizedCandidate;
    }

    private static string MergeRequiredText(bool isLatest, string? candidate, string fallback)
    {
        return MergeNullableText(isLatest, candidate, fallback) ?? string.Empty;
    }

    private static T? MergeNullableValue<T>(bool isLatest, T? candidate, T? fallback)
        where T : struct
    {
        return isLatest
            ? candidate ?? fallback
            : fallback ?? candidate;
    }
}
