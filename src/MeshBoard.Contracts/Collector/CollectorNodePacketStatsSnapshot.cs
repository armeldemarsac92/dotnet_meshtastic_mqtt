namespace MeshBoard.Contracts.Collector;

public sealed class CollectorNodePacketStatsSnapshot
{
    public DateTimeOffset GeneratedAtUtc { get; set; }

    public string WorkspaceId { get; set; } = string.Empty;

    public string? ServerAddress { get; set; }

    public string? Region { get; set; }

    public string? ChannelName { get; set; }

    public string? NodeId { get; set; }

    public string? PacketType { get; set; }

    public int LookbackHours { get; set; }

    public int RowCount { get; set; }

    public IReadOnlyCollection<CollectorNodePacketHourlyRollup> Rollups { get; set; } = [];
}
