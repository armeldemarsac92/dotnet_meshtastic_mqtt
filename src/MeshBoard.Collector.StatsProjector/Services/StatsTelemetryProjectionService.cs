using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.CollectorEvents.Normalized;
using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Collector.StatsProjector.Services;

public sealed class StatsTelemetryProjectionService : IStatsTelemetryProjectionService
{
    private readonly INodeRepository _nodeRepository;
    private readonly ILogger<StatsTelemetryProjectionService> _logger;

    public StatsTelemetryProjectionService(
        INodeRepository nodeRepository,
        ILogger<StatsTelemetryProjectionService> logger)
    {
        _nodeRepository = nodeRepository;
        _logger = logger;
    }

    public async Task ProjectAsync(TelemetryObserved telemetry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        _logger.LogDebug(
            "Projecting telemetry metric {MetricType} for node {NodeId}",
            telemetry.MetricType,
            telemetry.NodeId);

        var request = new UpsertObservedNodeRequest
        {
            NodeId = telemetry.NodeId,
            BrokerServer = telemetry.BrokerServer
        };

        var metricType = telemetry.MetricType.Trim();

        switch (metricType)
        {
            case "battery_level_percent":
                request.BatteryLevelPercent = (int)Math.Round(telemetry.MetricValue);
                break;
            case "voltage":
                request.Voltage = telemetry.MetricValue;
                break;
            case "channel_utilization":
                request.ChannelUtilization = telemetry.MetricValue;
                break;
            case "air_util_tx":
                request.AirUtilTx = telemetry.MetricValue;
                break;
            case "uptime_seconds":
                request.UptimeSeconds = (long)telemetry.MetricValue;
                break;
            case "temperature_celsius":
                request.TemperatureCelsius = telemetry.MetricValue;
                break;
            case "relative_humidity":
                request.RelativeHumidity = telemetry.MetricValue;
                break;
            case "barometric_pressure":
                request.BarometricPressure = telemetry.MetricValue;
                break;
            default:
                _logger.LogDebug(
                    "Skipping unsupported telemetry metric {MetricType} for node {NodeId}",
                    telemetry.MetricType,
                    telemetry.NodeId);
                return;
        }

        await _nodeRepository.UpsertAsync(request, ct);
    }
}
