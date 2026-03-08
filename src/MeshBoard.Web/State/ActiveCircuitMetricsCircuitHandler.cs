using MeshBoard.Application.Observability;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace MeshBoard.Web.State;

internal sealed class ActiveCircuitMetricsCircuitHandler : CircuitHandler
{
    private readonly IActiveCircuitMetricsService _activeCircuitMetricsService;
    private readonly ILogger<ActiveCircuitMetricsCircuitHandler> _logger;

    public ActiveCircuitMetricsCircuitHandler(
        IActiveCircuitMetricsService activeCircuitMetricsService,
        ILogger<ActiveCircuitMetricsCircuitHandler> logger)
    {
        _activeCircuitMetricsService = activeCircuitMetricsService;
        _logger = logger;
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _activeCircuitMetricsService.RecordCircuitOpened();

        _logger.LogDebug("Blazor circuit opened.");

        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _activeCircuitMetricsService.RecordCircuitClosed();

        _logger.LogDebug("Blazor circuit closed.");

        return Task.CompletedTask;
    }
}
