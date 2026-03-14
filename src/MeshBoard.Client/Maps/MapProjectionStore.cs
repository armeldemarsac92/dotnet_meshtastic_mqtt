using MeshBoard.Client.Nodes;
using MeshBoard.Client.Realtime;

namespace MeshBoard.Client.Maps;

public sealed class MapProjectionStore
{
    private const int MaxRetainedNodes = 1_000;
    private const int MaxRetainedActivityPulses = 128;
    private const int MaxPulseBurstPerNode = 4;
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
        _state.SetSnapshot(new());
    }

    public void SeedFromNodeProjections(IEnumerable<NodeProjectionEnvelope> nodeProjections)
    {
        ReplaceFromNodeProjections(nodeProjections, trackActivity: false);
    }

    public void ReconcileFromNodeProjections(IEnumerable<NodeProjectionEnvelope> nodeProjections)
    {
        ReplaceFromNodeProjections(nodeProjections, trackActivity: true);
    }

    public void Project(RealtimePacketWorkerResult packetResult)
    {
        ArgumentNullException.ThrowIfNull(packetResult);

        var projection = packetResult.DecodedPacket?.NodeProjection;
        if (projection is null)
        {
            return;
        }

        var nodeId = NormalizeRequiredText(projection.NodeId);
        if (nodeId.Length == 0)
        {
            return;
        }

        var current = _state.Snapshot;
        var existing = current.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        var next = BuildNodePoint(existing, projection, packetResult.RawPacket);

        if (next is null)
        {
            return;
        }

        var projectedAtUtc = ResolveProjectedAtUtc(packetResult.RawPacket?.ReceivedAtUtc, projection.LastHeardAtUtc, existing?.LastHeardAtUtc);
        var nodes = current.Nodes
            .Where(node => !string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
            .Append(next with
            {
                LastHeardAtUtc = projectedAtUtc
            })
            .OrderByDescending(node => node.LastHeardAtUtc ?? DateTimeOffset.MinValue)
            .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxRetainedNodes)
            .ToArray();
        var pulses = new[]
        {
            new MapNodeActivity
            {
                NodeId = next.NodeId,
                PulseCount = 1
            }
        };

        _state.SetSnapshot(current with
        {
            Nodes = nodes,
            ActivityPulses = pulses,
            LastProjectedAtUtc = projectedAtUtc,
            TotalProjected = current.TotalProjected + 1
        });
    }

    private void ReplaceFromNodeProjections(IEnumerable<NodeProjectionEnvelope> nodeProjections, bool trackActivity)
    {
        ArgumentNullException.ThrowIfNull(nodeProjections);

        var current = _state.Snapshot;
        var existingById = current.Nodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
        var nextNodes = new List<MapNodePoint>();
        var activityPulses = new List<MapNodeActivity>();
        DateTimeOffset? lastProjectedAtUtc = current.LastProjectedAtUtc;

        foreach (var projection in nodeProjections)
        {
            if (!TryCreateNodePoint(projection, out var nextNode))
            {
                continue;
            }

            lastProjectedAtUtc = Max(lastProjectedAtUtc, nextNode.LastHeardAtUtc);

            if (existingById.TryGetValue(nextNode.NodeId, out var existing))
            {
                nextNode = Merge(existing, nextNode);

                if (trackActivity)
                {
                    var pulseCount = Math.Min(
                        MaxPulseBurstPerNode,
                        Math.Max(0, projection.ObservedPacketCount - existing.ObservedPacketCount));

                    if (pulseCount > 0)
                    {
                        activityPulses.Add(new MapNodeActivity
                        {
                            NodeId = nextNode.NodeId,
                            PulseCount = pulseCount
                        });
                    }
                }
            }
            else if (trackActivity && projection.ObservedPacketCount > 0)
            {
                activityPulses.Add(new MapNodeActivity
                {
                    NodeId = nextNode.NodeId,
                    PulseCount = 1
                });
            }

            nextNodes.Add(nextNode);
        }

        var nodes = nextNodes
            .OrderByDescending(node => node.LastHeardAtUtc ?? DateTimeOffset.MinValue)
            .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxRetainedNodes)
            .ToArray();
        var pulses = activityPulses
            .OrderByDescending(activity => activity.PulseCount)
            .ThenBy(activity => activity.NodeId, StringComparer.OrdinalIgnoreCase)
            .Take(MaxRetainedActivityPulses)
            .ToArray();

        _state.SetSnapshot(current with
        {
            Nodes = nodes,
            ActivityPulses = pulses,
            LastProjectedAtUtc = lastProjectedAtUtc,
            TotalProjected = trackActivity
                ? current.TotalProjected + 1
                : current.TotalProjected
        });
    }

    private static MapNodePoint? BuildNodePoint(
        MapNodePoint? existing,
        RealtimeNodeProjectionEvent projection,
        RealtimeRawPacketEvent? rawPacket)
    {
        if (!TryResolveCoordinates(projection.LastKnownLatitude, projection.LastKnownLongitude, existing, out var latitude, out var longitude))
        {
            return null;
        }

        var observedPacketCount = existing is null
            ? 1
            : existing.ObservedPacketCount + 1;

        return new MapNodePoint
        {
            NodeId = NormalizeRequiredText(projection.NodeId),
            DisplayName = ResolveDisplayName(projection.LongName, projection.ShortName, existing?.DisplayName, projection.NodeId),
            Channel = PreferNormalizedText(projection.LastHeardChannel, existing?.Channel),
            Latitude = latitude,
            Longitude = longitude,
            BatteryLevelPercent = projection.BatteryLevelPercent ?? existing?.BatteryLevelPercent,
            BrokerServer = PreferNormalizedText(rawPacket?.BrokerServer, existing?.BrokerServer) ?? string.Empty,
            LastHeardAtUtc = ResolveProjectedAtUtc(rawPacket?.ReceivedAtUtc, projection.LastHeardAtUtc, existing?.LastHeardAtUtc),
            LastPacketType = PreferNormalizedText(projection.PacketType, existing?.LastPacketType),
            ObservedPacketCount = observedPacketCount
        };
    }

    private static bool TryCreateNodePoint(NodeProjectionEnvelope projection, out MapNodePoint point)
    {
        point = default!;

        var nodeId = NormalizeRequiredText(projection.NodeId);
        if (nodeId.Length == 0 ||
            !TryResolveCoordinates(projection.LastKnownLatitude, projection.LastKnownLongitude, existing: null, out var latitude, out var longitude))
        {
            return false;
        }

        point = new MapNodePoint
        {
            NodeId = nodeId,
            DisplayName = ResolveDisplayName(projection.LongName, projection.ShortName, fallbackDisplayName: null, nodeId),
            Channel = NormalizeNullableText(projection.LastHeardChannel),
            Latitude = latitude,
            Longitude = longitude,
            BatteryLevelPercent = projection.BatteryLevelPercent,
            BrokerServer = NormalizeRequiredText(projection.BrokerServer),
            LastHeardAtUtc = projection.LastHeardAtUtc,
            LastPacketType = NormalizeNullableText(projection.LastPacketType),
            ObservedPacketCount = Math.Max(0, projection.ObservedPacketCount)
        };
        return true;
    }

    private static MapNodePoint Merge(MapNodePoint existing, MapNodePoint candidate)
    {
        return candidate with
        {
            DisplayName = string.IsNullOrWhiteSpace(candidate.DisplayName) ? existing.DisplayName : candidate.DisplayName,
            Channel = PreferNormalizedText(candidate.Channel, existing.Channel),
            BatteryLevelPercent = candidate.BatteryLevelPercent ?? existing.BatteryLevelPercent,
            BrokerServer = string.IsNullOrWhiteSpace(candidate.BrokerServer) ? existing.BrokerServer : candidate.BrokerServer,
            LastHeardAtUtc = Max(existing.LastHeardAtUtc, candidate.LastHeardAtUtc),
            LastPacketType = PreferNormalizedText(candidate.LastPacketType, existing.LastPacketType),
            ObservedPacketCount = Math.Max(existing.ObservedPacketCount, candidate.ObservedPacketCount)
        };
    }

    private static bool TryResolveCoordinates(
        double? latitudeCandidate,
        double? longitudeCandidate,
        MapNodePoint? existing,
        out double latitude,
        out double longitude)
    {
        var resolvedLatitude = latitudeCandidate ?? existing?.Latitude;
        var resolvedLongitude = longitudeCandidate ?? existing?.Longitude;

        if (!resolvedLatitude.HasValue ||
            !resolvedLongitude.HasValue ||
            resolvedLatitude.Value is < -90 or > 90 ||
            resolvedLongitude.Value is < -180 or > 180)
        {
            latitude = default;
            longitude = default;
            return false;
        }

        latitude = resolvedLatitude.Value;
        longitude = resolvedLongitude.Value;
        return true;
    }

    private static DateTimeOffset ResolveProjectedAtUtc(
        DateTimeOffset? receivedAtUtc,
        DateTimeOffset projectionLastHeardAtUtc,
        DateTimeOffset? existingLastHeardAtUtc)
    {
        if (receivedAtUtc.HasValue && receivedAtUtc.Value != default)
        {
            return receivedAtUtc.Value;
        }

        if (projectionLastHeardAtUtc != default)
        {
            return projectionLastHeardAtUtc;
        }

        return existingLastHeardAtUtc ?? DateTimeOffset.UtcNow;
    }

    private static string ResolveDisplayName(
        string? longName,
        string? shortName,
        string? fallbackDisplayName,
        string fallbackNodeId)
    {
        return PreferNormalizedText(longName, shortName) ??
            NormalizeNullableText(fallbackDisplayName) ??
            NormalizeRequiredText(fallbackNodeId);
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

    private static string? PreferNormalizedText(string? candidate, string? fallback)
    {
        return NormalizeNullableText(candidate) ?? NormalizeNullableText(fallback);
    }
}
