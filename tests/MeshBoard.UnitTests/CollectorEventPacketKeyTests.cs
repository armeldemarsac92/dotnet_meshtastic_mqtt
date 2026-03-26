using MeshBoard.Contracts.CollectorEvents;

namespace MeshBoard.UnitTests;

public sealed class CollectorEventPacketKeyTests
{
    private static readonly DateTimeOffset ReceivedAtUtc = new(2026, 3, 26, 8, 15, 0, TimeSpan.Zero);

    [Fact]
    public void Build_WhenPacketIdAndFromNodeIdArePresent_ReturnsNodeScopedPacketKey()
    {
        var key = CollectorEventPacketKey.Build(
            "broker.meshboard.test",
            "!abcd1234",
            0x1234ABCD,
            "Text Message",
            "!beef5678",
            "hello",
            ReceivedAtUtc);

        Assert.Equal("broker.meshboard.test|!abcd1234:1234abcd", key);
    }

    [Fact]
    public void Build_WhenPacketIdIsNull_ReturnsSha256HexKey()
    {
        var key = CollectorEventPacketKey.Build(
            "broker.meshboard.test",
            "!abcd1234",
            null,
            "Text Message",
            "!beef5678",
            "hello",
            ReceivedAtUtc);

        Assert.Matches("^[0-9a-f]{64}$", key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WhenFromNodeIdIsMissingOrWhitespace_ReturnsSha256HexKey(string? fromNodeId)
    {
        var key = CollectorEventPacketKey.Build(
            "broker.meshboard.test",
            fromNodeId,
            0x1234ABCD,
            "Text Message",
            "!beef5678",
            "hello",
            ReceivedAtUtc);

        Assert.Matches("^[0-9a-f]{64}$", key);
    }

    [Fact]
    public void Build_WhenInputsAreIdentical_ReturnsDeterministicKey()
    {
        var first = CollectorEventPacketKey.Build(
            "broker.meshboard.test",
            "!abcd1234",
            null,
            "Telemetry",
            "!beef5678",
            "voltage=4.1",
            ReceivedAtUtc);
        var second = CollectorEventPacketKey.Build(
            "broker.meshboard.test",
            "!abcd1234",
            null,
            "Telemetry",
            "!beef5678",
            "voltage=4.1",
            ReceivedAtUtc);

        Assert.Equal(first, second);
    }
}
