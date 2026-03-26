namespace MeshBoard.Contracts.CollectorEvents.RawPackets;

public sealed class RawPacketReceived : CollectorEventMetadata
{
    public string Topic { get; init; } = string.Empty;

    public byte[] Payload { get; init; } = [];

    public DateTimeOffset ReceivedAtUtc { get; init; }

    public string CollectorInstanceId { get; init; } = string.Empty;
}
