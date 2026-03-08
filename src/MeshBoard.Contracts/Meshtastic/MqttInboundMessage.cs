namespace MeshBoard.Contracts.Meshtastic;

public sealed class MqttInboundMessage
{
    public string WorkspaceId { get; set; } = string.Empty;

    public string BrokerServer { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public byte[] Payload { get; set; } = [];

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
