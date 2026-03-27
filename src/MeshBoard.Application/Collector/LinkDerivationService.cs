using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Application.Collector;

public sealed class LinkDerivationService : ILinkDerivationService
{
    public IReadOnlyList<NeighborLinkRecord> DeriveLinks(MeshtasticEnvelope envelope)
    {
        var neighborLinks = BuildNeighborLinkRecords(envelope);
        var meshPacketLinks = BuildMeshPacketLinkRecords(envelope);
        var tracerouteLinks = BuildTracerouteLinkRecords(envelope);

        return neighborLinks
            .Concat(meshPacketLinks)
            .Concat(tracerouteLinks)
            .ToList();
    }

    private static IReadOnlyList<NeighborLinkRecord> BuildNeighborLinkRecords(MeshtasticEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.FromNodeId) || envelope.Neighbors is null || envelope.Neighbors.Count == 0)
        {
            return [];
        }

        var reportingNodeId = envelope.FromNodeId.Trim();
        var linksByKey = new Dictionary<string, NeighborLinkRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var neighbor in envelope.Neighbors)
        {
            if (string.IsNullOrWhiteSpace(neighbor.NodeId) ||
                string.Equals(reportingNodeId, neighbor.NodeId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var canonical = CreateCanonicalNeighborLink(
                reportingNodeId,
                neighbor.NodeId,
                neighbor.SnrDb,
                neighbor.LastRxAtUtc ?? envelope.ReceivedAtUtc);
            var linkKey = $"{canonical.SourceNodeId}|{canonical.TargetNodeId}";

            if (!linksByKey.TryGetValue(linkKey, out var existing))
            {
                linksByKey[linkKey] = canonical;
                continue;
            }

            var isIncomingLatest = canonical.LastSeenAtUtc >= existing.LastSeenAtUtc;
            linksByKey[linkKey] = new NeighborLinkRecord
            {
                SourceNodeId = existing.SourceNodeId,
                TargetNodeId = existing.TargetNodeId,
                SnrDb = isIncomingLatest
                    ? canonical.SnrDb ?? existing.SnrDb
                    : existing.SnrDb ?? canonical.SnrDb,
                LastSeenAtUtc = isIncomingLatest
                    ? canonical.LastSeenAtUtc
                    : existing.LastSeenAtUtc
            };
        }

        return linksByKey.Values.ToArray();
    }

    private static IReadOnlyList<NeighborLinkRecord> BuildMeshPacketLinkRecords(MeshtasticEnvelope envelope)
    {
        if (envelope.HopStart > 0 &&
            envelope.HopStart == envelope.HopLimit &&
            !string.IsNullOrWhiteSpace(envelope.FromNodeId) &&
            !string.IsNullOrWhiteSpace(envelope.GatewayNodeId) &&
            !string.Equals(envelope.FromNodeId, envelope.GatewayNodeId, StringComparison.OrdinalIgnoreCase))
        {
            var snr = envelope.RxSnr is not null && float.IsFinite(envelope.RxSnr.Value) ? envelope.RxSnr : null;

            return [CreateCanonicalNeighborLink(
                envelope.FromNodeId,
                envelope.GatewayNodeId,
                snr,
                envelope.ReceivedAtUtc)];
        }

        return [];
    }

    private static IReadOnlyList<NeighborLinkRecord> BuildTracerouteLinkRecords(MeshtasticEnvelope envelope)
    {
        if (envelope.TracerouteHops is null || envelope.TracerouteHops.Count < 2)
        {
            return [];
        }

        var hops = envelope.TracerouteHops;
        var linksByKey = new Dictionary<string, NeighborLinkRecord>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < hops.Count - 1; i++)
        {
            var sourceId = hops[i].NodeId;
            var targetId = hops[i + 1].NodeId;

            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
            {
                continue;
            }

            if (string.Equals(sourceId, targetId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var snr = hops[i + 1].SnrDb;
            var canonical = CreateCanonicalNeighborLink(sourceId, targetId, snr, envelope.ReceivedAtUtc);
            var linkKey = $"{canonical.SourceNodeId}|{canonical.TargetNodeId}";

            if (!linksByKey.ContainsKey(linkKey))
            {
                linksByKey[linkKey] = canonical;
            }
        }

        return linksByKey.Values.ToArray();
    }

    private static NeighborLinkRecord CreateCanonicalNeighborLink(
        string leftNodeId,
        string rightNodeId,
        float? snrDb,
        DateTimeOffset lastSeenAtUtc)
    {
        var normalizedLeft = leftNodeId.Trim();
        var normalizedRight = rightNodeId.Trim();
        var leftFirst = StringComparer.OrdinalIgnoreCase.Compare(normalizedLeft, normalizedRight) <= 0;

        return new NeighborLinkRecord
        {
            SourceNodeId = leftFirst ? normalizedLeft : normalizedRight,
            TargetNodeId = leftFirst ? normalizedRight : normalizedLeft,
            SnrDb = snrDb,
            LastSeenAtUtc = lastSeenAtUtc
        };
    }
}
