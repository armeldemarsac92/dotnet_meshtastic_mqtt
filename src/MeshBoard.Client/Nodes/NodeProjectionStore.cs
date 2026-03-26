using MeshBoard.Client.Realtime;
using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Client.Nodes;

public sealed class NodeProjectionStore
{
    private const int MaxRetainedNodes = 1_000;
    private readonly ProjectionPacketDeduper _packetDeduper = new(4_096);
    private readonly NodeProjectionState _state;

    public NodeProjectionStore(NodeProjectionState state)
    {
        _state = state;
    }

    public event Action? Changed
    {
        add => _state.Changed += value;
        remove => _state.Changed -= value;
    }

    public NodeProjectionSnapshot Current => _state.Snapshot;

    public void Clear()
    {
        _packetDeduper.Clear();
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
        var projection = packetResult.DecodedPacket.NodeProjection;

        if (string.IsNullOrWhiteSpace(projection.NodeId))
        {
            return;
        }

        if (!_packetDeduper.TryTrack(rawPacket, packetResult.DecodedPacket))
        {
            return;
        }

        var current = _state.Snapshot;
        var receivedAtUtc = projection.LastHeardAtUtc == default
            ? rawPacket.ReceivedAtUtc == default
                ? DateTimeOffset.UtcNow
                : rawPacket.ReceivedAtUtc
            : projection.LastHeardAtUtc;
        var existing = current.Nodes.FirstOrDefault(
            node => string.Equals(node.NodeId, projection.NodeId.Trim(), StringComparison.OrdinalIgnoreCase));
        var isLatest = existing is null ||
            !existing.LastHeardAtUtc.HasValue ||
            receivedAtUtc >= existing.LastHeardAtUtc.Value;
        var nextNode = existing is null
            ? CreateNode(projection, rawPacket, receivedAtUtc)
            : Merge(existing, projection, rawPacket, receivedAtUtc, isLatest);
        var nodes = current.Nodes
            .Where(node => !string.Equals(node.NodeId, nextNode.NodeId, StringComparison.OrdinalIgnoreCase))
            .Append(nextNode)
            .OrderBy(node => node, NodeProjectionEnvelopeComparer.Instance)
            .Take(MaxRetainedNodes)
            .ToArray();

        _state.SetSnapshot(current with
        {
            Nodes = nodes,
            LastProjectedAtUtc = receivedAtUtc,
            TotalProjected = current.TotalProjected + 1
        });
    }

    public static IReadOnlyList<NodeProjectionEnvelope> ApplyQuery(
        NodeProjectionSnapshot snapshot,
        NodeQuery query,
        ISet<string> favoriteNodeIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(favoriteNodeIds);

        return snapshot.Nodes
            .Where(node => MatchesQuery(node, query, favoriteNodeIds))
            .OrderBy(node => node, NodeProjectionEnvelopeComparer.For(query.SortBy))
            .ToArray();
    }

    private static NodeProjectionEnvelope CreateNode(
        RealtimeNodeProjectionEvent projection,
        RealtimeRawPacketEvent rawPacket,
        DateTimeOffset receivedAtUtc)
    {
        return new NodeProjectionEnvelope
        {
            NodeId = projection.NodeId.Trim(),
            BrokerServer = rawPacket.BrokerServer?.Trim() ?? string.Empty,
            ShortName = NormalizeNullableText(projection.ShortName),
            LongName = NormalizeNullableText(projection.LongName),
            LastHeardAtUtc = receivedAtUtc,
            LastHeardChannel = NormalizeNullableText(projection.LastHeardChannel),
            LastTextMessageAtUtc = projection.LastTextMessageAtUtc,
            LastPacketType = NormalizeNullableText(projection.PacketType),
            LastPayloadPreview = NormalizeNullableText(projection.PayloadPreview),
            LastKnownLatitude = projection.LastKnownLatitude,
            LastKnownLongitude = projection.LastKnownLongitude,
            BatteryLevelPercent = projection.BatteryLevelPercent,
            Voltage = projection.Voltage,
            ChannelUtilization = projection.ChannelUtilization,
            AirUtilTx = projection.AirUtilTx,
            UptimeSeconds = projection.UptimeSeconds,
            TemperatureCelsius = projection.TemperatureCelsius,
            RelativeHumidity = projection.RelativeHumidity,
            BarometricPressure = projection.BarometricPressure,
            ObservedPacketCount = 1
        };
    }

    private static NodeProjectionEnvelope Merge(
        NodeProjectionEnvelope existing,
        RealtimeNodeProjectionEvent projection,
        RealtimeRawPacketEvent rawPacket,
        DateTimeOffset receivedAtUtc,
        bool isLatest)
    {
        return existing with
        {
            BrokerServer = MergeRequiredText(isLatest, rawPacket.BrokerServer, existing.BrokerServer),
            ShortName = MergeNullableText(isLatest, projection.ShortName, existing.ShortName),
            LongName = MergeNullableText(isLatest, projection.LongName, existing.LongName),
            LastHeardAtUtc = Max(existing.LastHeardAtUtc, receivedAtUtc),
            LastHeardChannel = MergeNullableText(isLatest, projection.LastHeardChannel, existing.LastHeardChannel),
            LastTextMessageAtUtc = Max(existing.LastTextMessageAtUtc, projection.LastTextMessageAtUtc),
            LastPacketType = MergeNullableText(isLatest, projection.PacketType, existing.LastPacketType),
            LastPayloadPreview = MergeNullableText(isLatest, projection.PayloadPreview, existing.LastPayloadPreview),
            LastKnownLatitude = MergeNullableValue(isLatest, projection.LastKnownLatitude, existing.LastKnownLatitude),
            LastKnownLongitude = MergeNullableValue(isLatest, projection.LastKnownLongitude, existing.LastKnownLongitude),
            BatteryLevelPercent = MergeNullableValue(isLatest, projection.BatteryLevelPercent, existing.BatteryLevelPercent),
            Voltage = MergeNullableValue(isLatest, projection.Voltage, existing.Voltage),
            ChannelUtilization = MergeNullableValue(isLatest, projection.ChannelUtilization, existing.ChannelUtilization),
            AirUtilTx = MergeNullableValue(isLatest, projection.AirUtilTx, existing.AirUtilTx),
            UptimeSeconds = MergeNullableValue(isLatest, projection.UptimeSeconds, existing.UptimeSeconds),
            TemperatureCelsius = MergeNullableValue(isLatest, projection.TemperatureCelsius, existing.TemperatureCelsius),
            RelativeHumidity = MergeNullableValue(isLatest, projection.RelativeHumidity, existing.RelativeHumidity),
            BarometricPressure = MergeNullableValue(isLatest, projection.BarometricPressure, existing.BarometricPressure),
            ObservedPacketCount = existing.ObservedPacketCount + 1
        };
    }

    private static bool MatchesQuery(
        NodeProjectionEnvelope node,
        NodeQuery query,
        ISet<string> favoriteNodeIds)
    {
        if (query.OnlyFavorites && !favoriteNodeIds.Contains(node.NodeId))
        {
            return false;
        }

        if (query.OnlyWithLocation && !node.HasLocation)
        {
            return false;
        }

        if (query.OnlyWithTelemetry && !node.HasTelemetry)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.SearchText))
        {
            return true;
        }

        var filter = query.SearchText.Trim();

        return Contains(node.NodeId, filter) ||
            Contains(node.ShortName, filter) ||
            Contains(node.LongName, filter) ||
            Contains(node.LastHeardChannel, filter) ||
            Contains(node.LastPacketType, filter) ||
            Contains(node.LastPayloadPreview, filter);
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

    private sealed class NodeProjectionEnvelopeComparer : IComparer<NodeProjectionEnvelope>
    {
        public static readonly NodeProjectionEnvelopeComparer Instance = new(NodeSortOption.LastHeardDesc);

        private readonly NodeSortOption _sortOption;

        private NodeProjectionEnvelopeComparer(NodeSortOption sortOption)
        {
            _sortOption = sortOption;
        }

        public static NodeProjectionEnvelopeComparer For(NodeSortOption sortOption)
        {
            return sortOption == NodeSortOption.LastHeardDesc
                ? Instance
                : new NodeProjectionEnvelopeComparer(sortOption);
        }

        public int Compare(NodeProjectionEnvelope? left, NodeProjectionEnvelope? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return 1;
            }

            if (right is null)
            {
                return -1;
            }

            var comparison = _sortOption switch
            {
                NodeSortOption.NameAsc => CompareByName(left, right),
                NodeSortOption.BatteryDesc => CompareByBattery(left, right),
                _ => CompareByLastHeard(left, right)
            };

            return comparison != 0
                ? comparison
                : string.Compare(left.NodeId, right.NodeId, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareByName(NodeProjectionEnvelope left, NodeProjectionEnvelope right)
        {
            return string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareByBattery(NodeProjectionEnvelope left, NodeProjectionEnvelope right)
        {
            var comparison = Nullable.Compare(right.BatteryLevelPercent, left.BatteryLevelPercent);
            return comparison != 0 ? comparison : CompareByName(left, right);
        }

        private static int CompareByLastHeard(NodeProjectionEnvelope left, NodeProjectionEnvelope right)
        {
            var comparison = Nullable.Compare(right.LastHeardAtUtc, left.LastHeardAtUtc);
            return comparison != 0 ? comparison : CompareByName(left, right);
        }
    }
}
