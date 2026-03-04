namespace MeshBoard.Contracts.Meshtastic;

public sealed class MqttInboundMessage
{
    public string Topic { get; set; } = string.Empty;

    public byte[] Payload { get; set; } = [];

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
