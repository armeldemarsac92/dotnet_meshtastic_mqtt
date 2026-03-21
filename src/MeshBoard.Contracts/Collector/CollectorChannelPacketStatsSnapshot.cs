namespace MeshBoard.Contracts.Collector;

public sealed class CollectorChannelPacketStatsSnapshot
{
    public DateTimeOffset GeneratedAtUtc { get; set; }

    public string WorkspaceId { get; set; } = string.Empty;

    public string? ServerAddress { get; set; }

    public string? Region { get; set; }

    public string? ChannelName { get; set; }

    public string? PacketType { get; set; }

    public int LookbackHours { get; set; }

    public int RowCount { get; set; }

    public IReadOnlyCollection<CollectorChannelPacketHourlyRollup> Rollups { get; set; } = [];
}
