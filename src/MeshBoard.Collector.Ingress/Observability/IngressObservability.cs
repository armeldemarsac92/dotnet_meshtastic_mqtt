using System.Diagnostics.Metrics;
using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Collector.Ingress.Observability;

internal static class IngressObservability
{
    private static readonly Meter Meter = new("MeshBoard.Collector.Ingress");
    private static readonly Counter<long> PublishedRawPacketCounter = Meter.CreateCounter<long>(
        "meshboard.collector.ingress.raw_packets.published",
        description: "Number of raw packet events published to Kafka.");
    private static readonly Counter<long> PublishFailureCounter = Meter.CreateCounter<long>(
        "meshboard.collector.ingress.raw_packets.publish_failures",
        description: "Number of raw packet publish attempts that failed.");
    private static readonly Histogram<double> PublishLagMilliseconds = Meter.CreateHistogram<double>(
        "meshboard.collector.ingress.raw_packets.lag.ms",
        unit: "ms",
        description: "Elapsed time between MQTT receipt and Kafka publication.");

    private static int _consecutiveFailureCount;
    private static long _lastFailureUnixTimeMilliseconds;
    private static long _lastLagMilliseconds;
    private static long _lastSuccessUnixTimeMilliseconds;
    private static string _lastFailureTopic = string.Empty;

    public static void RecordPublishSucceeded(MqttInboundMessage inboundMessage)
    {
        ArgumentNullException.ThrowIfNull(inboundMessage);

        var now = DateTimeOffset.UtcNow;
        var lagMilliseconds = Math.Max(0d, (now - inboundMessage.ReceivedAtUtc).TotalMilliseconds);

        PublishLagMilliseconds.Record(lagMilliseconds);
        PublishedRawPacketCounter.Add(1);

        Interlocked.Exchange(
            ref _lastLagMilliseconds,
            Convert.ToInt64(Math.Round(lagMilliseconds, MidpointRounding.AwayFromZero)));
        Interlocked.Exchange(ref _lastSuccessUnixTimeMilliseconds, now.ToUnixTimeMilliseconds());
        Interlocked.Exchange(ref _consecutiveFailureCount, 0);
        Interlocked.Exchange(ref _lastFailureTopic, string.Empty);
    }

    public static void RecordPublishFailure(MqttInboundMessage inboundMessage)
    {
        ArgumentNullException.ThrowIfNull(inboundMessage);

        PublishFailureCounter.Add(1);

        Interlocked.Increment(ref _consecutiveFailureCount);
        Interlocked.Exchange(ref _lastFailureUnixTimeMilliseconds, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Interlocked.Exchange(ref _lastFailureTopic, inboundMessage.Topic);
    }

    public static IngressPublishHealthSnapshot CreateHealthSnapshot()
    {
        var lastSuccessUnixTimeMilliseconds = Interlocked.Read(ref _lastSuccessUnixTimeMilliseconds);
        var lastFailureUnixTimeMilliseconds = Interlocked.Read(ref _lastFailureUnixTimeMilliseconds);
        var lastFailureTopic = Interlocked.CompareExchange(ref _lastFailureTopic, string.Empty, string.Empty);

        return new IngressPublishHealthSnapshot(
            Interlocked.CompareExchange(ref _consecutiveFailureCount, 0, 0),
            Interlocked.Read(ref _lastLagMilliseconds),
            lastSuccessUnixTimeMilliseconds == 0
                ? null
                : DateTimeOffset.FromUnixTimeMilliseconds(lastSuccessUnixTimeMilliseconds),
            lastFailureUnixTimeMilliseconds == 0
                ? null
                : DateTimeOffset.FromUnixTimeMilliseconds(lastFailureUnixTimeMilliseconds),
            lastFailureTopic);
    }
}

internal readonly record struct IngressPublishHealthSnapshot(
    int ConsecutiveFailureCount,
    long LastLagMilliseconds,
    DateTimeOffset? LastSuccessAtUtc,
    DateTimeOffset? LastFailureAtUtc,
    string LastFailureTopic);
