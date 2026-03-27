using System.Globalization;
using MeshBoard.Client.Maps;
using MeshBoard.Contracts.Collector;
using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Client.Collector;

public static class CollectorMapViewBuilder
{
    private const int CoordinateBucketPrecision = 5;
    private const int OverlapRingSlotCount = 8;
    private const double OverlapRingSpacingMeters = 35;

    public static IReadOnlyList<CollectorMapNodeView> BuildNodeViews(
        IReadOnlyCollection<NodeSummary>? nodes)
    {
        if (nodes is null || nodes.Count == 0)
        {
            return [];
        }

        return nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.NodeId) &&
                           node.LastKnownLatitude.HasValue &&
                           node.LastKnownLongitude.HasValue)
            .Select(node => new CollectorMapNodeView
            {
                NodeId = node.NodeId.Trim(),
                BrokerServer = node.BrokerServer?.Trim() ?? string.Empty,
                ShortName = NormalizeNullable(node.ShortName),
                LongName = NormalizeNullable(node.LongName),
                ChannelKey = NormalizeNullable(node.LastHeardChannel),
                Latitude = node.LastKnownLatitude!.Value,
                Longitude = node.LastKnownLongitude!.Value,
                BatteryLevelPercent = node.BatteryLevelPercent,
                LastHeardAtUtc = node.LastHeardAtUtc
            })
            .OrderByDescending(node => node.LastHeardAtUtc)
            .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<CollectorMapNodeView> ApplySearch(
        IReadOnlyCollection<CollectorMapNodeView>? nodes,
        string? searchText)
    {
        if (nodes is null || nodes.Count == 0)
        {
            return [];
        }

        var normalizedSearchText = searchText?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSearchText))
        {
            return nodes
                .OrderByDescending(node => node.LastHeardAtUtc)
                .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return nodes
            .Where(node => Contains(node.NodeId, normalizedSearchText) ||
                           Contains(node.ShortName, normalizedSearchText) ||
                           Contains(node.LongName, normalizedSearchText) ||
                           Contains(node.ChannelKey, normalizedSearchText) ||
                           Contains(node.BrokerServer, normalizedSearchText))
            .OrderByDescending(node => node.LastHeardAtUtc)
            .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<MapNodePoint> ToMapNodePoints(IReadOnlyCollection<CollectorMapNodeView> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var points = new List<MapNodePoint>(nodes.Count);

        foreach (var group in nodes
                     .GroupBy(CreateCoordinateBucketKey, StringComparer.Ordinal)
                     .Select(group => group
                         .OrderByDescending(node => node.LastHeardAtUtc)
                         .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
                         .ToArray()))
        {
            if (group.Length == 1)
            {
                points.Add(CreatePoint(group[0], group[0].Latitude, group[0].Longitude));
                continue;
            }

            for (var index = 0; index < group.Length; index += 1)
            {
                var node = group[index];
                var (latitude, longitude) = ApplyOverlapOffset(node.Latitude, node.Longitude, index, group.Length);
                points.Add(CreatePoint(node, latitude, longitude));
            }
        }

        return points;
    }

    public static int CountCoordinateBuckets(IReadOnlyCollection<CollectorMapNodeView> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        return nodes
            .Select(CreateCoordinateBucketKey)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    public static IReadOnlyList<RadioLinkPoint> BuildRadioLinkPoints(
        IReadOnlyCollection<CollectorMapLinkSummary>? links,
        IReadOnlyCollection<CollectorMapNodeView> visibleNodes)
    {
        if (links is null || links.Count == 0 || visibleNodes.Count == 0)
        {
            return [];
        }

        var visibleNodesById = visibleNodes
            .DistinctBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);

        return links
            .Select(link => TryResolveCoordinates(link, visibleNodesById))
            .OfType<RadioLinkPoint>()
            .ToArray();
    }

    private static RadioLinkPoint? TryResolveCoordinates(
        CollectorMapLinkSummary link,
        IReadOnlyDictionary<string, CollectorMapNodeView> visibleNodesById)
    {
        if (!visibleNodesById.TryGetValue(link.SourceNodeId, out var sourceNode) ||
            !visibleNodesById.TryGetValue(link.TargetNodeId, out var targetNode))
        {
            return null;
        }

        return new RadioLinkPoint
        {
            SourceNodeId = link.SourceNodeId,
            TargetNodeId = link.TargetNodeId,
            SnrDb = link.SnrDb,
            SourceLatitude = sourceNode.Latitude,
            SourceLongitude = sourceNode.Longitude,
            TargetLatitude = targetNode.Latitude,
            TargetLongitude = targetNode.Longitude
        };
    }

    private static MapNodePoint CreatePoint(CollectorMapNodeView node, double latitude, double longitude)
    {
        return new MapNodePoint
        {
            NodeId = node.NodeId,
            DisplayName = node.DisplayName,
            Channel = node.ChannelKey,
            Latitude = latitude,
            Longitude = longitude,
            BatteryLevelPercent = node.BatteryLevelPercent,
            BrokerServer = node.BrokerServer,
            LastHeardAtUtc = node.LastHeardAtUtc
        };
    }

    private static (double Latitude, double Longitude) ApplyOverlapOffset(
        double latitude,
        double longitude,
        int groupIndex,
        int groupSize)
    {
        if (groupSize <= 1)
        {
            return (latitude, longitude);
        }

        var ringIndex = groupIndex / OverlapRingSlotCount;
        var slotIndex = groupIndex % OverlapRingSlotCount;
        var slotsInRing = Math.Min(OverlapRingSlotCount, groupSize - (ringIndex * OverlapRingSlotCount));
        var angle = (2 * Math.PI * slotIndex) / Math.Max(1, slotsInRing);
        var radiusMeters = OverlapRingSpacingMeters * (ringIndex + 1);

        var latitudeOffsetDegrees = (radiusMeters * Math.Sin(angle)) / 111_320d;
        var longitudeScale = Math.Cos(latitude * Math.PI / 180d);
        var longitudeOffsetDegrees = Math.Abs(longitudeScale) < 0.00001d
            ? 0d
            : (radiusMeters * Math.Cos(angle)) / (111_320d * longitudeScale);

        return (latitude + latitudeOffsetDegrees, longitude + longitudeOffsetDegrees);
    }

    private static string CreateCoordinateBucketKey(CollectorMapNodeView node)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Math.Round(node.Latitude, CoordinateBucketPrecision):F5}|{Math.Round(node.Longitude, CoordinateBucketPrecision):F5}");
    }

    private static bool Contains(string? source, string filter)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               source.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
