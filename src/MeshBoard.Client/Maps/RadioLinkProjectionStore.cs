using MeshBoard.Client.Realtime;

namespace MeshBoard.Client.Maps;

public sealed class RadioLinkProjectionStore
{
    private static readonly TimeSpan StalenessWindow = TimeSpan.FromHours(2);
    private readonly Dictionary<string, RadioLinkEnvelope> _links = new(StringComparer.OrdinalIgnoreCase);
    private readonly ProjectionPacketDeduper _packetDeduper = new(4_096);

    public int ObservedReportCount { get; private set; }

    public int ObservedNeighborCount { get; private set; }

    public DateTimeOffset? LastObservedAtUtc { get; private set; }

    public event Action? Changed;

    public IReadOnlyList<RadioLinkEnvelope> Current => _links.Values
        .OrderByDescending(link => link.LastSeenAtUtc)
        .ThenBy(link => link.SourceNodeId, StringComparer.OrdinalIgnoreCase)
        .ThenBy(link => link.TargetNodeId, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public void Clear()
    {
        _packetDeduper.Clear();
        var hadState = _links.Count > 0 || ObservedReportCount > 0 || ObservedNeighborCount > 0 || LastObservedAtUtc.HasValue;
        ObservedReportCount = 0;
        ObservedNeighborCount = 0;
        LastObservedAtUtc = null;

        if (_links.Count == 0)
        {
            if (hadState)
            {
                Changed?.Invoke();
            }

            return;
        }

        _links.Clear();
        Changed?.Invoke();
    }

    public void Project(RealtimePacketWorkerResult packetResult)
    {
        ArgumentNullException.ThrowIfNull(packetResult);

        if (packetResult.RawPacket is null || packetResult.DecodedPacket?.NeighborInfo is null)
        {
            return;
        }

        if (!_packetDeduper.TryTrack(packetResult.RawPacket, packetResult.DecodedPacket))
        {
            return;
        }

        var rawPacket = packetResult.RawPacket;
        var neighborInfo = packetResult.DecodedPacket.NeighborInfo;
        var observedAtUtc = rawPacket.ReceivedAtUtc == default
            ? DateTimeOffset.UtcNow
            : rawPacket.ReceivedAtUtc;
        ObservedReportCount += 1;
        ObservedNeighborCount += neighborInfo.Neighbors.Count;
        LastObservedAtUtc = observedAtUtc;
        var reportingNodeId = NormalizeNodeId(neighborInfo.ReportingNodeId);
        var changed = PruneStaleLinks(observedAtUtc);

        if (reportingNodeId is null)
        {
            if (changed)
            {
                Changed?.Invoke();
            }

            return;
        }

        foreach (var neighbor in neighborInfo.Neighbors)
        {
            var targetNodeId = NormalizeNodeId(neighbor.NodeId);
            if (targetNodeId is null || string.Equals(reportingNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lastSeenAtUtc = neighbor.LastRxAtUtc ?? observedAtUtc;
            if (observedAtUtc - lastSeenAtUtc > StalenessWindow)
            {
                continue;
            }

            var nextLink = CreateCanonicalEnvelope(reportingNodeId, targetNodeId, neighbor.SnrDb, lastSeenAtUtc);
            changed |= Upsert(nextLink);
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private bool Upsert(RadioLinkEnvelope nextLink)
    {
        var linkKey = CreateLinkKey(nextLink.SourceNodeId, nextLink.TargetNodeId);

        if (!_links.TryGetValue(linkKey, out var existing))
        {
            _links[linkKey] = nextLink;
            return true;
        }

        var isIncomingLatest = nextLink.LastSeenAtUtc >= existing.LastSeenAtUtc;
        var merged = new RadioLinkEnvelope(
            existing.SourceNodeId,
            existing.TargetNodeId,
            isIncomingLatest
                ? nextLink.SnrDb ?? existing.SnrDb
                : existing.SnrDb ?? nextLink.SnrDb,
            isIncomingLatest
                ? nextLink.LastSeenAtUtc
                : existing.LastSeenAtUtc);

        if (merged == existing)
        {
            return false;
        }

        _links[linkKey] = merged;
        return true;
    }

    private bool PruneStaleLinks(DateTimeOffset referenceTimeUtc)
    {
        var staleLinkKeys = _links
            .Where(entry => referenceTimeUtc - entry.Value.LastSeenAtUtc > StalenessWindow)
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var staleLinkKey in staleLinkKeys)
        {
            _links.Remove(staleLinkKey);
        }

        return staleLinkKeys.Length > 0;
    }

    private static RadioLinkEnvelope CreateCanonicalEnvelope(
        string leftNodeId,
        string rightNodeId,
        float? snrDb,
        DateTimeOffset lastSeenAtUtc)
    {
        var isLeftFirst = StringComparer.OrdinalIgnoreCase.Compare(leftNodeId, rightNodeId) <= 0;

        return new RadioLinkEnvelope(
            isLeftFirst ? leftNodeId : rightNodeId,
            isLeftFirst ? rightNodeId : leftNodeId,
            snrDb,
            lastSeenAtUtc);
    }

    private static string CreateLinkKey(string leftNodeId, string rightNodeId)
    {
        var isLeftFirst = StringComparer.OrdinalIgnoreCase.Compare(leftNodeId, rightNodeId) <= 0;
        return isLeftFirst
            ? $"{leftNodeId}|{rightNodeId}"
            : $"{rightNodeId}|{leftNodeId}";
    }

    private static string? NormalizeNodeId(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }
}
