namespace MeshBoard.Contracts.Topics;

public sealed class ChannelSummary
{
    public int PacketCount { get; set; }

    public int UniqueSenderCount { get; set; }

    public int DecodedPacketCount { get; set; }

    public DateTimeOffset? LastSeenAtUtc { get; set; }

    public IReadOnlyCollection<string> ObservedBrokerServers { get; set; } = [];
}
