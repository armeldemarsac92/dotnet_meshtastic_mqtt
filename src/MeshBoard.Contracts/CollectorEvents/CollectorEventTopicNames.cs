namespace MeshBoard.Contracts.CollectorEvents;

public static class CollectorEventTopicNames
{
    public const string RawPackets = "collector.raw-packets.v1";

    public const string PacketNormalized = "collector.packet-normalized.v1";

    public const string NodeObserved = "collector.node-observed.v1";

    public const string LinkObserved = "collector.link-observed.v1";

    public const string TelemetryObserved = "collector.telemetry-observed.v1";

    public const string DeadLetter = "collector.dead-letter.v1";
}
