using MeshBoard.Contracts.Diagnostics;

namespace MeshBoard.Application.Observability;

public sealed class InMemoryActiveCircuitMetricsService : IActiveCircuitMetricsService
{
    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;

    private int _activeCircuitCount;
    private long _closedCircuitCount;
    private DateTimeOffset? _lastChangedAtUtc;
    private long _openedCircuitCount;
    private int _peakActiveCircuitCount;

    public InMemoryActiveCircuitMetricsService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public ActiveCircuitMetricSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new ActiveCircuitMetricSnapshot
            {
                ActiveCircuitCount = _activeCircuitCount,
                PeakActiveCircuitCount = _peakActiveCircuitCount,
                OpenedCircuitCount = _openedCircuitCount,
                ClosedCircuitCount = _closedCircuitCount,
                LastChangedAtUtc = _lastChangedAtUtc
            };
        }
    }

    public void RecordCircuitClosed()
    {
        lock (_sync)
        {
            _closedCircuitCount++;
            _activeCircuitCount = Math.Max(0, _activeCircuitCount - 1);
            _lastChangedAtUtc = _timeProvider.GetUtcNow();
        }
    }

    public void RecordCircuitOpened()
    {
        lock (_sync)
        {
            _openedCircuitCount++;
            _activeCircuitCount++;
            _peakActiveCircuitCount = Math.Max(_peakActiveCircuitCount, _activeCircuitCount);
            _lastChangedAtUtc = _timeProvider.GetUtcNow();
        }
    }
}
