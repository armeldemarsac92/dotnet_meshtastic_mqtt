using MeshBoard.Application.Observability;

namespace MeshBoard.UnitTests;

public sealed class ActiveCircuitMetricsServiceTests
{
    [Fact]
    public void RecordCircuitOpened_AndClosed_ShouldTrackActiveAndPeakCounts()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 8, 12, 0, 0, TimeSpan.Zero));
        var service = new InMemoryActiveCircuitMetricsService(timeProvider);

        service.RecordCircuitOpened();
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        service.RecordCircuitOpened();
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        service.RecordCircuitClosed();

        var snapshot = service.GetSnapshot();

        Assert.Equal(1, snapshot.ActiveCircuitCount);
        Assert.Equal(2, snapshot.PeakActiveCircuitCount);
        Assert.Equal(2, snapshot.OpenedCircuitCount);
        Assert.Equal(1, snapshot.ClosedCircuitCount);
        Assert.Equal(new DateTimeOffset(2026, 3, 8, 12, 0, 10, TimeSpan.Zero), snapshot.LastChangedAtUtc);
    }

    [Fact]
    public void RecordCircuitClosed_ShouldNotAllowNegativeActiveCount()
    {
        var service = new InMemoryActiveCircuitMetricsService(TimeProvider.System);

        service.RecordCircuitClosed();
        service.RecordCircuitClosed();

        var snapshot = service.GetSnapshot();

        Assert.Equal(0, snapshot.ActiveCircuitCount);
        Assert.Equal(0, snapshot.PeakActiveCircuitCount);
        Assert.Equal(0, snapshot.OpenedCircuitCount);
        Assert.Equal(2, snapshot.ClosedCircuitCount);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
