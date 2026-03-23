using MeshBoard.Contracts.Nodes;
using MeshBoard.Infrastructure.Persistence.SQL.Requests;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class ObservedNodeMapping
{
    public static UpsertObservedNodeSqlRequest ToSqlRequest(this UpsertObservedNodeRequest request)
    {
        return new UpsertObservedNodeSqlRequest
        {
            WorkspaceId = string.Empty,
            NodeId = request.NodeId,
            BrokerServer = request.BrokerServer,
            ShortName = request.ShortName,
            LongName = request.LongName,
            LastHeardAtUtc = request.LastHeardAtUtc?.ToString("O"),
            LastHeardChannel = request.LastHeardChannel,
            LastTextMessageAtUtc = request.LastTextMessageAtUtc?.ToString("O"),
            LastKnownLatitude = request.LastKnownLatitude,
            LastKnownLongitude = request.LastKnownLongitude,
            BatteryLevelPercent = request.BatteryLevelPercent,
            Voltage = request.Voltage,
            ChannelUtilization = request.ChannelUtilization,
            AirUtilTx = request.AirUtilTx,
            UptimeSeconds = request.UptimeSeconds,
            TemperatureCelsius = request.TemperatureCelsius,
            RelativeHumidity = request.RelativeHumidity,
            BarometricPressure = request.BarometricPressure
        };
    }
}
