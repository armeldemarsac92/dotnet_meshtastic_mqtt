using System.Diagnostics.Metrics;

namespace MeshBoard.Collector.StatsProjector.Observability;

internal static class StatsProjectorObservability
{
    private static readonly Meter Meter = new("MeshBoard.Collector.StatsProjector");
    private static readonly Counter<long> PacketsProjectedCounter = Meter.CreateCounter<long>(
        "meshboard.collector.stats_projector.packets.projected",
        description: "Number of packet records projected into the stats store.");
    private static readonly Counter<long> NodesUpsertedCounter = Meter.CreateCounter<long>(
        "meshboard.collector.stats_projector.nodes.upserted",
        description: "Number of node projections upserted into the stats store.");
    private static readonly Counter<long> LinksUpsertedCounter = Meter.CreateCounter<long>(
        "meshboard.collector.stats_projector.links.upserted",
        description: "Number of link projections upserted into the stats store.");
    private static readonly Histogram<double> TransactionMilliseconds = Meter.CreateHistogram<double>(
        "meshboard.collector.stats_projector.transaction.ms",
        unit: "ms",
        description: "Elapsed time for successful stats projection transactions.");

    public static void RecordPacketProjected()
    {
        PacketsProjectedCounter.Add(1);
    }

    public static void RecordNodeUpserted()
    {
        NodesUpsertedCounter.Add(1);
    }

    public static void RecordLinkUpserted()
    {
        LinksUpsertedCounter.Add(1);
    }

    public static void RecordTransactionCompleted(double elapsedMilliseconds)
    {
        TransactionMilliseconds.Record(elapsedMilliseconds);
    }
}
