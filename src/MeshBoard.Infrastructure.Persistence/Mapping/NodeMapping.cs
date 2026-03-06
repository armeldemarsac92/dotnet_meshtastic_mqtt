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
            BrokerServer = response.BrokerServer,
            ShortName = response.ShortName,
            LongName = response.LongName,
            LastHeardAtUtc = ParseNullableDateTimeOffset(response.LastHeardAtUtc),
            LastHeardChannel = response.LastHeardChannel,
            LastTextMessageAtUtc = ParseNullableDateTimeOffset(response.LastTextMessageAtUtc),
            LastKnownLatitude = response.LastKnownLatitude,
            LastKnownLongitude = response.LastKnownLongitude,
            BatteryLevelPercent = response.BatteryLevelPercent,
            Voltage = response.Voltage,
            ChannelUtilization = response.ChannelUtilization,
            AirUtilTx = response.AirUtilTx,
            UptimeSeconds = response.UptimeSeconds,
            TemperatureCelsius = response.TemperatureCelsius,
            RelativeHumidity = response.RelativeHumidity,
            BarometricPressure = response.BarometricPressure
        };
    }

    private static DateTimeOffset? ParseNullableDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedValue)
            ? parsedValue
            : null;
    }
}
