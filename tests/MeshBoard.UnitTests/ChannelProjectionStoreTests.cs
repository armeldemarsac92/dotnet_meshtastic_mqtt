using MeshBoard.Client.Channels;
using MeshBoard.Client.Realtime;

namespace MeshBoard.UnitTests;

public sealed class ChannelProjectionStoreTests
{
    [Fact]
    public void Project_ShouldCreateObservedChannelFromMeshtasticTopic()
    {
        var store = new ChannelProjectionStore(new ChannelProjectionState());

        store.Project(CreatePacketResult());

        var channel = Assert.Single(store.Current.Channels);
        Assert.Equal("broker.meshboard.test|US/LongFast", channel.Id);
        Assert.Equal("US/LongFast", channel.ChannelKey);
        Assert.Equal("Text Message", channel.LastPacketType);
        Assert.Equal("hello mesh", channel.LastPayloadPreview);
        Assert.Equal(1, channel.ObservedPacketCount);
        Assert.Equal(1, channel.DistinctNodeCount);
    }

    [Fact]
    public void Project_ShouldMergeRepeatedObservationsAndTrackDistinctNodes()
    {
        var store = new ChannelProjectionStore(new ChannelProjectionState());

        store.Project(CreatePacketResult(nodeId: "!0000162e", nodeNumber: 5678, packetType: "Text Message", payloadPreview: "hello mesh"));
        store.Project(CreatePacketResult(nodeId: "!0000162f", nodeNumber: 5679, packetType: "Telemetry", payloadPreview: "Device metrics: 96% battery"));

        var channel = Assert.Single(store.Current.Channels);
        Assert.Equal(2, channel.ObservedPacketCount);
        Assert.Equal(2, channel.DistinctNodeCount);
        Assert.Equal("Telemetry", channel.LastPacketType);
        Assert.Equal("Device metrics: 96% battery", channel.LastPayloadPreview);
    }

    [Fact]
    public void Project_WhenTopicIsNotMeshtastic_ShouldIgnorePacket()
    {
        var store = new ChannelProjectionStore(new ChannelProjectionState());

        store.Project(CreatePacketResult(sourceTopic: "meshboard/workspaces/workspace-a/live/packets"));

        Assert.Empty(store.Current.Channels);
    }

    private static RealtimePacketWorkerResult CreatePacketResult(
        string sourceTopic = "msh/US/2/e/LongFast/!0000162e",
        string nodeId = "!0000162e",
        uint nodeNumber = 5678,
        string packetType = "Text Message",
        string payloadPreview = "hello mesh")
    {
        return new RealtimePacketWorkerResult
        {
            IsSuccess = true,
            DecryptResultClassification = RealtimePacketWorkerDecryptResultClassifications.Decrypted,
            RawPacket = new RealtimeRawPacketEvent
            {
                WorkspaceId = "workspace-a",
                BrokerServer = "broker.meshboard.test",
                SourceTopic = sourceTopic,
                DownstreamTopic = "meshboard/workspaces/workspace-a/live/packets",
                PayloadBase64 = "AQID",
                PayloadSizeBytes = 3,
                ReceivedAtUtc = DateTimeOffset.Parse("2026-03-14T17:00:00Z"),
                IsEncrypted = true,
                FromNodeNumber = nodeNumber,
                PacketId = 42
            },
            DecodedPacket = new RealtimeDecodedPacketEvent
            {
                PortNumValue = 1,
                PortNumName = "TEXT_MESSAGE_APP",
                PacketType = packetType,
                PayloadBase64 = "AQID",
                PayloadSizeBytes = 3,
                PayloadPreview = payloadPreview,
                SourceNodeNumber = nodeNumber,
                NodeProjection = new RealtimeNodeProjectionEvent
                {
                    NodeId = nodeId,
                    NodeNumber = nodeNumber,
                    LastHeardAtUtc = DateTimeOffset.Parse("2026-03-14T17:00:00Z"),
                    LastHeardChannel = "US/LongFast",
                    PacketType = packetType,
                    PayloadPreview = payloadPreview
                }
            }
        };
    }
}
