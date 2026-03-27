using MeshBoard.Contracts.CollectorEvents;

namespace MeshBoard.UnitTests;

public sealed class PacketClassifierTests
{
    [Theory]
    [InlineData("Text Message", CollectorDecodeStatus.Succeeded)]
    [InlineData("Compressed Text Message", CollectorDecodeStatus.Succeeded)]
    [InlineData("Position Update", CollectorDecodeStatus.Succeeded)]
    [InlineData("Node Info", CollectorDecodeStatus.Succeeded)]
    [InlineData("Routing", CollectorDecodeStatus.Succeeded)]
    [InlineData("Telemetry", CollectorDecodeStatus.Succeeded)]
    [InlineData("Traceroute", CollectorDecodeStatus.Succeeded)]
    [InlineData("Neighbor Info", CollectorDecodeStatus.Succeeded)]
    [InlineData("Encrypted Packet", CollectorDecodeStatus.Failed)]
    [InlineData("Unknown Packet", CollectorDecodeStatus.Failed)]
    [InlineData("SomeOtherType", CollectorDecodeStatus.UnsupportedPayload)]
    [InlineData(null, CollectorDecodeStatus.UnsupportedPayload)]
    public void ResolveDecodeStatus_ReturnsExpectedStatus(string? packetType, CollectorDecodeStatus expected)
    {
        var status = PacketClassifier.ResolveDecodeStatus(packetType);

        Assert.Equal(expected, status);
    }

    [Theory]
    [InlineData("Encrypted Packet", CollectorDecryptStatus.Failed)]
    [InlineData("Text Message", CollectorDecryptStatus.NotRequired)]
    [InlineData(null, CollectorDecryptStatus.NotRequired)]
    public void ResolveDecryptStatus_ReturnsExpectedStatus(string? packetType, CollectorDecryptStatus expected)
    {
        var status = PacketClassifier.ResolveDecryptStatus(packetType);

        Assert.Equal(expected, status);
    }

    [Theory]
    [InlineData(true, false, CollectorLinkOrigin.NeighborInfo)]
    [InlineData(false, true, CollectorLinkOrigin.Traceroute)]
    [InlineData(false, false, CollectorLinkOrigin.MeshPacket)]
    [InlineData(true, true, CollectorLinkOrigin.NeighborInfo)]
    public void ResolveLinkOrigin_ReturnsExpectedOrigin(
        bool hasNeighbors,
        bool hasMultiHopTraceroute,
        CollectorLinkOrigin expected)
    {
        var origin = PacketClassifier.ResolveLinkOrigin(hasNeighbors, hasMultiHopTraceroute);

        Assert.Equal(expected, origin);
    }
}
