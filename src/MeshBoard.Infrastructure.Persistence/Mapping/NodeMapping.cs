using System.Globalization;
using MeshBoard.Contracts.Nodes;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class NodeMapping
{
    public static IReadOnlyCollection<NodeSummary> MapToNodes(this IReadOnlyCollection<NodeSqlResponse> responses)
    {
        return responses.Select(MapToNode).ToList();
    }

    private static NodeSummary MapToNode(NodeSqlResponse response)
    {
        return new NodeSummary
        {
            NodeId = response.NodeId,
            ShortName = response.ShortName,
            LongName = response.LongName,
            LastHeardAtUtc = ParseNullableDateTimeOffset(response.LastHeardAtUtc),
            LastTextMessageAtUtc = ParseNullableDateTimeOffset(response.LastTextMessageAtUtc),
            LastKnownLatitude = response.LastKnownLatitude,
            LastKnownLongitude = response.LastKnownLongitude
        };
    }

    private static DateTimeOffset? ParseNullableDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
