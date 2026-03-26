using System.Diagnostics.Metrics;

namespace MeshBoard.Collector.Normalizer.Observability;

internal static class NormalizerObservability
{
    private static readonly Meter Meter = new("MeshBoard.Collector.Normalizer");
    private static readonly Counter<long> DecodedPacketsCounter = Meter.CreateCounter<long>(
        "meshboard.collector.normalizer.packets.decoded",
        description: "Number of normalized packet events published successfully.");
    private static readonly Counter<long> DeadLetteredPacketsCounter = Meter.CreateCounter<long>(
        "meshboard.collector.normalizer.packets.dead_lettered",
        description: "Number of packets routed to dead letter.");
    private static readonly Counter<long> DecryptFailedPacketsCounter = Meter.CreateCounter<long>(
        "meshboard.collector.normalizer.packets.decrypt_failed",
        description: "Number of normalized packets that could not be decrypted.");

    public static void RecordDecodeSucceeded()
    {
        DecodedPacketsCounter.Add(1);
    }

    public static void RecordDeadLettered()
    {
        DeadLetteredPacketsCounter.Add(1);
    }

    public static void RecordDecryptFailed()
    {
        DecryptFailedPacketsCounter.Add(1);
    }
}
