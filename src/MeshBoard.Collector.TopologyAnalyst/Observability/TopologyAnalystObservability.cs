using System.Diagnostics.Metrics;

namespace MeshBoard.Collector.TopologyAnalyst.Observability;

internal static class TopologyAnalystObservability
{
    private static readonly Meter Meter = new("MeshBoard.Collector.TopologyAnalyst");
    private static readonly Counter<long> SuccessfulRunsCounter = Meter.CreateCounter<long>(
        "meshboard.collector.topology_analyst.runs.succeeded",
        description: "Number of topology analysis runs that completed successfully.");
    private static readonly Counter<long> FailedRunsCounter = Meter.CreateCounter<long>(
        "meshboard.collector.topology_analyst.runs.failed",
        description: "Number of topology analysis runs that failed.");
    private static readonly Histogram<double> RunMilliseconds = Meter.CreateHistogram<double>(
        "meshboard.collector.topology_analyst.run.ms",
        unit: "ms",
        description: "Elapsed time for topology analysis runs.");

    public static void RecordRunSucceeded(double elapsedMilliseconds)
    {
        SuccessfulRunsCounter.Add(1);
        RunMilliseconds.Record(elapsedMilliseconds);
    }

    public static void RecordRunFailed(double elapsedMilliseconds)
    {
        FailedRunsCounter.Add(1);
        RunMilliseconds.Record(elapsedMilliseconds);
    }
}
