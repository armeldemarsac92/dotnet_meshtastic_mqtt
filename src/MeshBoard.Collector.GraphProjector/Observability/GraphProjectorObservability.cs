using System.Diagnostics.Metrics;

namespace MeshBoard.Collector.GraphProjector.Observability;

internal static class GraphProjectorObservability
{
    private static readonly Meter Meter = new("MeshBoard.Collector.GraphProjector");
    private static readonly Counter<long> NodesUpsertedCounter = Meter.CreateCounter<long>(
        "meshboard.collector.graph_projector.nodes.upserted",
        description: "Number of node projections written to Neo4j.");
    private static readonly Counter<long> LinksUpsertedCounter = Meter.CreateCounter<long>(
        "meshboard.collector.graph_projector.links.upserted",
        description: "Number of link projections written to Neo4j.");
    private static readonly Histogram<double> WriteMilliseconds = Meter.CreateHistogram<double>(
        "meshboard.collector.graph_projector.write.ms",
        unit: "ms",
        description: "Elapsed time for successful graph writes.");

    public static void RecordNodeUpserted()
    {
        NodesUpsertedCounter.Add(1);
    }

    public static void RecordLinkUpserted()
    {
        LinksUpsertedCounter.Add(1);
    }

    public static void RecordWriteCompleted(double elapsedMilliseconds)
    {
        WriteMilliseconds.Record(elapsedMilliseconds);
    }
}
