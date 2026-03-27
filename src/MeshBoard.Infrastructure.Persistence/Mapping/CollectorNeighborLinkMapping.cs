using System.Globalization;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class CollectorNeighborLinkMapping
{
    public static IReadOnlyList<NeighborLinkRecord> MapToNeighborLinkRecords(
        this IEnumerable<NeighborLinkSqlResponse> responses)
    {
        return responses.Select(MapToNeighborLinkRecord).ToArray();
    }

    public static NeighborLinkRecord MapToNeighborLinkRecord(this NeighborLinkSqlResponse response)
    {
        return new NeighborLinkRecord
        {
            SourceNodeId = response.SourceNodeId,
            TargetNodeId = response.TargetNodeId,
            SnrDb = response.SnrDb,
            LastSeenAtUtc = DateTimeOffset.Parse(
                response.LastSeenAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
        };
    }
}
