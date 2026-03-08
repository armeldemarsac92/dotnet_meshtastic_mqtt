namespace MeshBoard.Contracts.Topics;

public sealed class ChannelTopNode
{
    public string NodeId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int PacketCount { get; set; }
}
