namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class CollectorChannelPacketHourlyRollupSqlResponse
{
    public required string BucketStartUtc { get; set; }

    public required string ServerAddress { get; set; }

    public required string Region { get; set; }

    public required string MeshVersion { get; set; }

    public required string ChannelName { get; set; }

    public required string PacketType { get; set; }

    public int PacketCount { get; set; }

    public int ActiveNodeCount { get; set; }

    public required string FirstSeenAtUtc { get; set; }

    public required string LastSeenAtUtc { get; set; }
}
