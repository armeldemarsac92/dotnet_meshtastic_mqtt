using MeshBoard.Contracts.CollectorEvents;
using MeshBoard.Contracts.CollectorEvents.Normalized;
using MeshBoard.Contracts.CollectorEvents.RawPackets;

namespace MeshBoard.UnitTests;

public sealed class CollectorEventTopicNamesTests
{
    [Fact]
    public void TopicNames_ShouldMatchExpectedConstants()
    {
        Assert.Equal("collector.raw-packets.v1", CollectorEventTopicNames.RawPackets);
        Assert.Equal("collector.packet-normalized.v1", CollectorEventTopicNames.PacketNormalized);
        Assert.Equal("collector.node-observed.v1", CollectorEventTopicNames.NodeObserved);
        Assert.Equal("collector.link-observed.v1", CollectorEventTopicNames.LinkObserved);
        Assert.Equal("collector.telemetry-observed.v1", CollectorEventTopicNames.TelemetryObserved);
        Assert.Equal("collector.dead-letter.v1", CollectorEventTopicNames.DeadLetter);
    }

    [Fact]
    public void BuildChannelScope_ShouldReturnStablePartitionKey()
    {
        var partitionKey = CollectorEventPartitionKeys.BuildChannelScope(
            "broker.meshboard.test",
            "msh/US/2/json/LongFast/#");

        Assert.Equal(
            "broker.meshboard.test|msh/US/2/json/LongFast/#",
            partitionKey);
    }

    [Theory]
    [InlineData("", "topic/#")]
    [InlineData("broker.meshboard.test", "")]
    [InlineData("broker|meshboard.test", "topic/#")]
    [InlineData("broker.meshboard.test", "topic|/#")]
    public void BuildChannelScope_WhenASegmentIsInvalid_ShouldThrow(
        string brokerServer,
        string topicPattern)
    {
        Assert.Throws<ArgumentException>(() =>
            CollectorEventPartitionKeys.BuildChannelScope(brokerServer, topicPattern));
    }

    [Fact]
    public void RawPacketReceived_ShouldDefaultToSchemaVersionV1()
    {
        var contract = new RawPacketReceived();

        Assert.Equal(CollectorEventSchemaVersions.V1, contract.SchemaVersion);
        Assert.Empty(contract.Payload);
    }

    [Fact]
    public void PacketNormalized_ShouldDefaultToEmptyCollections()
    {
        var contract = new PacketNormalized();

        Assert.Empty(contract.Neighbors);
        Assert.Empty(contract.TracerouteHops);
    }
}
