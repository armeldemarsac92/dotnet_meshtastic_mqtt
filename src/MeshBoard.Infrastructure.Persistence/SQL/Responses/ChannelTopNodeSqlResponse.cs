namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class ChannelTopNodeSqlResponse
{
    public string NodeId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int PacketCount { get; set; }
}
