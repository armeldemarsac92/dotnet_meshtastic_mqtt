using MeshBoard.RealtimeLoadTests.Configuration;
using MeshBoard.RealtimeLoadTests.Load;

namespace MeshBoard.UnitTests;

public sealed class RealtimeLoadRunSummaryTests
{
    [Fact]
    public void Create_ShouldBuildPercentilesAndFailureBreakdown()
    {
        var summary = RealtimeLoadRunSummary.Create(
            RealtimeLoadScenario.ConnectBurst,
            new RealtimeLoadTestOptions
            {
                ClientCount = 4,
                MaxConcurrency = 2
            },
            DateTimeOffset.Parse("2026-03-19T09:00:00Z"),
            DateTimeOffset.Parse("2026-03-19T09:00:05Z"),
            [
                new RealtimeLoadSample("connect", true, 10),
                new RealtimeLoadSample("connect", true, 20),
                new RealtimeLoadSample("connect", true, 30),
                new RealtimeLoadSample("connect", false, 40, "timed out")
            ]);

        var metric = Assert.Single(summary.Metrics);
        Assert.Equal("connect", metric.Operation);
        Assert.Equal(4, metric.Attempted);
        Assert.Equal(3, metric.Succeeded);
        Assert.Equal(1, metric.Failed);
        Assert.Equal(10, metric.MinLatencyMs);
        Assert.Equal(25, metric.MeanLatencyMs);
        Assert.Equal(40, metric.MaxLatencyMs);
        Assert.Equal(20, metric.P50LatencyMs);
        Assert.Equal(40, metric.P95LatencyMs);
        Assert.Equal(40, metric.P99LatencyMs);
        Assert.Equal(["timed out"], metric.FailureReasons);
    }
}
