namespace MeshBoard.Contracts.Messages;

public sealed class MessageQuery
{
    public string BrokerServer { get; set; } = string.Empty;

    public string SearchText { get; set; } = string.Empty;

    public string PacketType { get; set; } = string.Empty;

    public MessageVisibilityFilter Visibility { get; set; } = MessageVisibilityFilter.All;
}
