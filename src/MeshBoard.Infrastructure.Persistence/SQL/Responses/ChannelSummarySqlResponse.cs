namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class ChannelSummarySqlResponse
{
    public int PacketCount { get; set; }

    public int UniqueSenderCount { get; set; }

    public int DecodedPacketCount { get; set; }

    public string? LastSeenAtUtc { get; set; }

    public string? BrokerServersCsv { get; set; }
}
